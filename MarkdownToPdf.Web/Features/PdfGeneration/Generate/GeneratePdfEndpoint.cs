using Carter;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using MarkdownToPdf.Web.Shared.Constants;
using MarkdownToPdf.Web.Shared.Http;
using MarkdownToPdf.Web.Shared.Validation;

namespace MarkdownToPdf.Web.Features.PdfGeneration.Generate;

public sealed class GeneratePdfEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(string.Empty)
                       .WithTags(Api.Tags.PdfGeneration);

        group.MapPost(Api.Routes.PdfGeneration.Generate, HandleGenerateAsync)
            .RequireRateLimiting("PdfGenerationPolicy"); // Attaches the specific policy

        group.MapGet($"{Api.Routes.PdfGeneration.Prefix}/download/{{fileId:guid}}", HandleDownloadAsync);
    }

    private static async Task<IResult> HandleGenerateAsync(
        [FromForm] GeneratePdfCommand command,
        [FromServices] ISender sender,
        [FromServices] IMemoryCache cache,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error is ValidationError validationError)
            {
                return HtmxResults.ErrorAlert(validationError.Errors.Select(e => e.Message));
            }
            return HtmxResults.ErrorAlert(result.Error.Message);
        }

        var fileId = Guid.NewGuid();
        cache.Set(fileId, result.Value!, TimeSpan.FromMinutes(5));

        // NATIVE HTMX DOWNLOAD PATTERN:
        // HTMX intercepts this header and redirects the client's browser to the download URL.
        // Because the target URL returns a file, the browser downloads it without refreshing the page.
        httpContext.Response.Headers.Append("HX-Redirect", $"{Api.Routes.PdfGeneration.Prefix}/download/{fileId}");

        return Results.Ok();
    }

    private static IResult HandleDownloadAsync(
        [FromRoute] Guid fileId,
        [FromServices] IMemoryCache cache)
    {
        if (cache.TryGetValue(fileId, out byte[]? pdfBytes) && pdfBytes is not null)
        {
            return Results.File(pdfBytes, "application/pdf", "MarkdownDocument.pdf");
        }

        return Results.NotFound("The download link has expired. Please generate the PDF again.");
    }
}