using Carter;
using MarkdownToPdf.Web.Extensions;
using Serilog;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// ARCHITECTURAL FIX: builder.Host.UseSerilog() freezes the global static Log.Logger. 
// In parallel xUnit integration tests, multiple threads attempt to freeze this same global logger simultaneously, 
// causing the "logger is already frozen" crash. 
// Using builder.Services.AddSerilog() strictly scopes the logger to the application's DI container, 
// completely bypassing the static state and allowing parallel tests to run safely.
builder.Services.AddSerilog((services, configuration) => configuration
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddEnterpriseInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseSerilogRequestLogging();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseRateLimiter(); // Enforce rate limits immediately after routing
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapCarter();
app.MapRazorPages();

var assemblyName = Assembly.GetExecutingAssembly().GetName().Name;

// Instead of calling the static Log.Information, we use the 
// Microsoft.Extensions.Logging.ILogger resolved from the built application's DI container.
app.Logger.LogInformation(
    "\n====================================================================\n" +
    "  APPLICATION BOOTSTRAPPING: {AssemblyName}\n" +
    "  START TIME: {StartTime}\n" +
    "====================================================================",
    assemblyName, DateTimeOffset.Now);

app.Run();

public partial class Program { }