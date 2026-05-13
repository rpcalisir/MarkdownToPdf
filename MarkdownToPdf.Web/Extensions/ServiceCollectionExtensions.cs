using Carter;
using FluentValidation;
using MarkdownToPdf.Web.Infrastructure.Database;
using MarkdownToPdf.Web.Shared.Logging;
using MarkdownToPdf.Web.Shared.Validation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog.Core;

namespace MarkdownToPdf.Web.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEnterpriseInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Centralize the assembly reference so both MediatR and FluentValidation scan the exact same place
        var applicationAssembly = typeof(Program).Assembly;

        // 1. Register MediatR and the Pipeline Behaviors
        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(applicationAssembly);

            // ARCHITECTURAL RULE: Order matters immensely here.
            // LoggingBehavior MUST be registered FIRST. MediatR executes behaviors in the order they are added.
            // By wrapping ValidationBehavior inside LoggingBehavior, if a Validation failure occurs,
            // the LoggingBehavior will catch the returning Result.Failure and log it properly.
            config.AddOpenBehavior(typeof(LoggingBehavior<,>));
            config.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        // 2. Register FluentValidation (THIS IS WHAT WAKES THE BOUNCER UP)
        services.AddValidatorsFromAssembly(applicationAssembly);

        // 3. Register Carter for Minimal APIs
        services.AddCarter();

        // 4. Logging & Context Requirements
        // Enables access to the HTTP Context across the application (required for our Enricher)
        services.AddHttpContextAccessor();

        // Registers our custom enricher into DI. Serilog's .ReadFrom.Services() will automatically pick this up.
        services.AddTransient<ILogEventEnricher, HtmxLogEnricher>();

        // 5. Register Razor Pages and Components
        services.AddRazorPages();
        services.AddRazorComponents();

        // 6. Add Explicit Antiforgery Services for Minimal APIs
        services.AddAntiforgery();

        // 7. Database Registration
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"),
                sqlOptions =>
                {
                    // Enterprise-grade transient fault handling
                    // If the database drops the connection, do not crash immediately. Try to reconnect 3 times, waiting 10 seconds between each try.
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorNumbersToAdd: null);
                }));

        // 8. Identity Registration
        services.AddIdentity<IdentityUser, IdentityRole>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequireUppercase = true;
            options.Password.RequiredLength = 8;

            options.User.RequireUniqueEmail = true;
        })
        // Fluent Builder Pattern
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        // 
        services.AddMemoryCache();
        services.AddScoped<Microsoft.AspNetCore.Components.Web.HtmlRenderer>();

        return services;
    }
}