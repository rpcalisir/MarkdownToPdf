using Carter;
using FluentValidation;
using MarkdownToPdf.Web.Features.PdfGeneration;
using MarkdownToPdf.Web.Features.PdfGeneration.Jobs;
using MarkdownToPdf.Web.Infrastructure.Database;
using MarkdownToPdf.Web.Shared.Configuration;
using MarkdownToPdf.Web.Shared.Logging;
using MarkdownToPdf.Web.Shared.Validation;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Serilog.Core;
using System.Threading.RateLimiting;

namespace MarkdownToPdf.Web.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEnterpriseInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var applicationAssembly = typeof(Program).Assembly;

        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(applicationAssembly);
            config.AddOpenBehavior(typeof(LoggingBehavior<,>));
            config.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(applicationAssembly);
        services.AddCarter();
        services.AddHttpContextAccessor();
        services.AddTransient<ILogEventEnricher, HtmxLogEnricher>();
        services.AddRazorPages();
        services.AddRazorComponents();
        services.AddAntiforgery();

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"),
                sqlOptions => sqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null)));

        services.AddIdentity<IdentityUser, IdentityRole>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequireUppercase = true;
            options.Password.RequiredLength = 8;
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        services.AddMemoryCache();
        services.AddScoped<Microsoft.AspNetCore.Components.Web.HtmlRenderer>();
        services.AddSingleton<IPdfService, PuppeteerPdfService>();

        services.AddSingleton<IPdfGenerationQueue, PdfGenerationQueue>();
        services.AddHostedService<PdfGenerationWorker>();

        services.ConfigureRateLimiting(configuration);

        // ARCHITECTURAL FIX: Replaced obsolete KnownNetworks with KnownIPNetworks.
        // This ensures the application correctly extracts the true user IP address 
        // when deployed behind the reverse proxy of a free hosting provider.
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        return services;
    }

    private static IServiceCollection ConfigureRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<PdfGenerationRateLimiterPolicy>();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy<string, PdfGenerationRateLimiterPolicy>("PdfGenerationPolicy");
        });

        return services;
    }
}

public class PdfGenerationRateLimiterPolicy : IRateLimiterPolicy<string>
{
    private readonly RateLimitingSettings _settings;

    public PdfGenerationRateLimiterPolicy(IConfiguration configuration)
    {
        _settings = new RateLimitingSettings();
        configuration.GetSection(RateLimitingSettings.SectionName).Bind(_settings);

        OnRejected = async (context, cancellationToken) =>
        {
            context.HttpContext.Response.Headers.RetryAfter = _settings.WindowSeconds.ToString();

            var result = new RazorComponentResult(
                typeof(MarkdownToPdf.Web.Shared.Components.ErrorAlert),
                new { Errors = new List<string> { "You've reached your request limit. Please wait 60 seconds before trying again." } }
            )
            {
                StatusCode = StatusCodes.Status429TooManyRequests
            };

            await result.ExecuteAsync(context.HttpContext);
        };
    }

    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected { get; }

    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown_ip";

        var syncIoFeature = httpContext.Features.Get<IHttpBodyControlFeature>();
        if (syncIoFeature != null)
        {
            syncIoFeature.AllowSynchronousIO = true;
        }

        if (httpContext.Request.HasFormContentType)
        {
            var form = httpContext.Request.Form;

            if (!form.TryGetValue("MarkdownText", out var text) || string.IsNullOrWhiteSpace(text))
            {
                return RateLimitPartition.GetNoLimiter("bypass_" + ipAddress);
            }
        }

        return RateLimitPartition.GetFixedWindowLimiter(ipAddress, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = _settings.PermitLimit,
            Window = TimeSpan.FromSeconds(_settings.WindowSeconds),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    }
}