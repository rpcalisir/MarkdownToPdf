using Carter;
using FluentValidation;
using MarkdownToPdf.Web.Features.PdfGeneration;
using MarkdownToPdf.Web.Infrastructure.Database;
using MarkdownToPdf.Web.Shared.Configuration;
using MarkdownToPdf.Web.Shared.Http;
using MarkdownToPdf.Web.Shared.Logging;
using MarkdownToPdf.Web.Shared.Validation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
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

        services.ConfigureRateLimiting(configuration);

        return services;
    }

    private static IServiceCollection ConfigureRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var rateLimitConfig = new RateLimitingSettings();
        configuration.GetSection(RateLimitingSettings.SectionName).Bind(rateLimitConfig);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy("PdfGenerationPolicy", context =>
            {
                var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown_ip";

                return RateLimitPartition.GetFixedWindowLimiter(ipAddress, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = rateLimitConfig.PermitLimit,
                    Window = TimeSpan.FromSeconds(rateLimitConfig.WindowSeconds),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
            });

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = rateLimitConfig.WindowSeconds.ToString();

                // ARCHITECTURAL FIX: Our shared 'HtmxResults.ErrorAlert' factory internally forces a 400 Bad Request status code.
                // When the Rate Limiter executes it, it silently overwrites the 429 status code back to 400, breaking the HTTP contract.
                // We bypass the factory and explicitly instantiate the RazorComponentResult, binding it to the 429 status.
                var result = new RazorComponentResult(
                    typeof(MarkdownToPdf.Web.Shared.Components.ErrorAlert),
                    new { Errors = new List<string> { "Rate limit exceeded. Please wait one minute before generating another PDF." } }
                )
                {
                    StatusCode = StatusCodes.Status429TooManyRequests
                };

                await result.ExecuteAsync(context.HttpContext);
            };
        });

        return services;
    }
}