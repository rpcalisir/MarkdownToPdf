using Microsoft.AspNetCore.Http.HttpResults;
using MarkdownToPdf.Web.Shared.Components; 

namespace MarkdownToPdf.Web.Shared.Http;

/// <summary>
/// Enterprise factory for generating standardized HTMX/Razor Component responses.
/// </summary>
public static class HtmxResults
{
    public static IResult ErrorAlert(string message)
    {
        return new RazorComponentResult<ErrorAlert>(
            new { Errors = new List<string> { message } })
        {
            StatusCode = 400,
            ContentType = "text/html" // Explicitly enforce HTML for HTMX
        };
    }

    public static IResult ErrorAlert(IEnumerable<string> messages)
    {
        return new RazorComponentResult<ErrorAlert>(
            new { Errors = messages.ToList() })
        {
            StatusCode = 400,
            ContentType = "text/html" // Explicitly enforce HTML for HTMX
        };
    }

    public static IResult SuccessAlert(string message)
    {
        return new RazorComponentResult<SuccessAlert>(
            new { Message = message })
        {
            // Best practice to enforce it on success responses as well
            ContentType = "text/html"
        };
    }
}