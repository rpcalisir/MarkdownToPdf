using Serilog.Core;
using Serilog.Events;
using MarkdownToPdf.Web.Shared.Constants;

namespace MarkdownToPdf.Web.Shared.Logging;

/// <summary>
/// Enriches the log context with specific HTMX headers to trace partial DOM swaps and UI triggers.
/// This prevents losing the client-side context in server-side logs.
/// </summary>
public sealed class HtmxLogEnricher(IHttpContextAccessor httpContextAccessor) : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var context = httpContextAccessor.HttpContext;
        if (context is null) return;

        if (context.Request.Headers.TryGetValue(DiagnosticKeys.HtmxRequestHeader, out var hxRequest))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(DiagnosticKeys.HtmxRequestLogProperty, hxRequest.ToString()));
        }

        if (context.Request.Headers.TryGetValue(DiagnosticKeys.HtmxTargetHeader, out var hxTarget))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(DiagnosticKeys.HtmxTargetLogProperty, hxTarget.ToString()));
        }
    }
}