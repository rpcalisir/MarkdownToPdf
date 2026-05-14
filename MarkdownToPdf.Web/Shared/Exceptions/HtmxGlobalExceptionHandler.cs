namespace MarkdownToPdf.Web.Shared.Exceptions;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using MarkdownToPdf.Web.Shared.Constants;

/// <summary>
/// Intercepts unhandled exceptions globally. If the request was made via HTMX, 
/// it safely returns a Razor Component toast instead of breaking the DOM with a full HTML error page.
/// </summary>
public sealed class HtmxGlobalExceptionHandler(ILogger<HtmxGlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // 1. Log the critical failure so we can debug it later
        logger.LogError(exception, "A critical unhandled exception occurred during the request.");

        // 2. Check if the request came from HTMX
        if (httpContext.Request.Headers.ContainsKey(DiagnosticKeys.HtmxRequestHeader))
        {
            // By default, HTMX ignores 500 errors. We return 200 OK to force the DOM to render the toast.
            httpContext.Response.StatusCode = StatusCodes.Status200OK;

            // HX-Retarget forces the toast into the correct container, even if the original request 
            // was targeting a different div (like your preview-pane).
            httpContext.Response.Headers["HX-Retarget"] = "#toast-container";
            httpContext.Response.Headers["HX-Reswap"] = "innerHTML";

            // 3. Render the safe UI toast
            var result = new RazorComponentResult(
                typeof(MarkdownToPdf.Web.Shared.Components.ErrorAlert),
                new { Errors = new List<string> { "A critical system error occurred. Please try again later." } }
            );

            await result.ExecuteAsync(httpContext);

            // Tell .NET we successfully handled the exception
            return true;
        }

        // 4. Tell .NET to pass the exception to the default /Error page (for non-HTMX requests)
        return false;
    }
}