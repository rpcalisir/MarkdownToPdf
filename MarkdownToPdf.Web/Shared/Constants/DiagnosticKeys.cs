namespace MarkdownToPdf.Web.Shared.Constants;

/// <summary>
/// Centralized keys used for logging context, tracing, and telemetry.
/// Prevents magic strings and ensures consistency across indexing platforms like Seq/Datadog.
/// </summary>
public static class DiagnosticKeys
{
    public const string RequestName = "MediatRRequestName";

    // Centralizes HTMX headers to prevent magic strings across the application
    public const string HtmxRequestHeader = "HX-Request";
    public const string HtmxTargetHeader = "HX-Target";

    // Standardized log property names for telemetry indexing
    public const string HtmxRequestLogProperty = "HtmxRequest";
    public const string HtmxTargetLogProperty = "HtmxTarget";
}