using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using MarkdownToPdf.Web.Shared.Constants;
using MarkdownToPdf.Web.Shared.Http;
using MarkdownToPdf.Web.Shared.Validation;
using MarkdownToPdf.Web.Shared.Components;
using MarkdownToPdf.Web.Features.PdfGeneration.Jobs;

namespace MarkdownToPdf.Web.Features.PdfGeneration.Generate;

public sealed class GeneratePdfEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(string.Empty)
                       .WithTags(Api.Tags.PdfGeneration);

        // ARCHITECTURAL FIX: Removed the redundant .RequireAntiforgery() call. 
        // .NET 8 Minimal APIs automatically validate antiforgery tokens for any endpoint 
        // configured with a [FromForm] parameter when the middleware is active.
        group.MapPost(Api.Routes.PdfGeneration.Generate, HandleGenerateAsync)
             .RequireRateLimiting("PdfGenerationPolicy");

        group.MapGet($"{Api.Routes.PdfGeneration.Prefix}/status/{{jobId:guid}}", HandleStatusAsync);

        group.MapGet($"{Api.Routes.PdfGeneration.Prefix}/download/{{fileId:guid}}", HandleDownloadAsync);
    }

    private static async Task<IResult> HandleGenerateAsync(
        [FromForm] GeneratePdfCommand command,
        [FromServices] ISender sender,
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

        httpContext.Response.StatusCode = StatusCodes.Status202Accepted;
        return new RazorComponentResult(typeof(ProcessingAlert), new { JobId = result.Value });
    }

    private static IResult HandleStatusAsync(
        [FromRoute] Guid jobId,
        [FromServices] IMemoryCache cache,
        HttpContext httpContext)
    {
        if (!cache.TryGetValue(jobId, out PdfJobState? state) || state is null)
        {
            return HtmxResults.ErrorAlert("Your session expired. Please generate the PDF again.");
        }

        // ARCHITECTURAL FIX: Rely entirely on HTMX HTML DOM Swapping.
        // Swap the spinner for the Success UI containing the hidden iFrame.
        return state.Status switch
        {
            JobStatus.Pending or JobStatus.Processing => new RazorComponentResult(typeof(ProcessingAlert), new { JobId = jobId }),

            JobStatus.Completed => new RazorComponentResult(typeof(DownloadReadyAlert), new { DownloadUrl = $"{Api.Routes.PdfGeneration.Prefix}/download/{jobId}" }),

            JobStatus.Failed => HtmxResults.ErrorAlert(state.ErrorMessage ?? "Failed to generate PDF."),

            _ => HtmxResults.ErrorAlert("Unknown job status.")
        };
    }

    private static IResult HandleDownloadAsync(
        [FromRoute] Guid fileId,
        [FromServices] IMemoryCache cache)
    {
        if (cache.TryGetValue(fileId, out PdfJobState? state) && state?.PdfBytes is not null)
        {
            return Results.File(state.PdfBytes, "application/pdf", "MarkdownDocument.pdf");
        }

        return Results.NotFound("The download link has expired. Please generate the PDF again.");
    }
}