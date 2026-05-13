using Carter;
using MarkdownToPdf.Web.Extensions;
using Serilog;
using System.Reflection;

// 1. Bootstrap Serilog immediately to catch configuration or startup errors (Console ONLY)
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Full Serilog Integration via appsettings.json (This is where the FILE logger wakes up)
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddEnterpriseInfrastructure(builder.Configuration);

    var app = builder.Build();

    // Replaces noisy Microsoft HTTP logs with a single, clean log per request
    app.UseSerilogRequestLogging();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();

    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseAntiforgery();

    app.MapCarter();
    app.MapRazorPages();

    // ENTERPRISE STARTUP BANNER: 
    // Logged AFTER the builder is built, ensuring it writes to both Console and File.
    // Notice we use \n here instead of {NewLine} for proper string formatting.
    var assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
    Log.Information(
        "\n====================================================================\n" +
        "  APPLICATION BOOTSTRAPPING: {AssemblyName}\n" +
        "  START TIME: {StartTime}\n" +
        "====================================================================",
        assemblyName, DateTimeOffset.Now);

    app.Run();
}
catch (Exception ex)
{
    // If DI or the Database fails to configure, it is caught here and sent to the logging sink
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    // Ensures all logs are flushed to the sink before the application dies
    Log.CloseAndFlush();
}

public partial class Program { }