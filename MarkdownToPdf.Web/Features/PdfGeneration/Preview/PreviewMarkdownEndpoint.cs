using Carter;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using MarkdownToPdf.Web.Shared.Constants;
using MarkdownToPdf.Web.Features.PdfGeneration.Preview.Templates;

namespace MarkdownToPdf.Web.Features.PdfGeneration.Preview;

public sealed class PreviewMarkdownEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost($"{Api.Routes.PdfGeneration.Prefix}/preview", HandlePreviewAsync)
           .WithTags(Api.Tags.PdfGeneration);
    }

    private static async Task<IResult> HandlePreviewAsync(
        [FromForm] PreviewMarkdownCommand command,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return new RazorComponentResult<MarkdownPreviewTemplate>(new { HtmlContent = string.Empty });
        }

        return new RazorComponentResult<MarkdownPreviewTemplate>(new { HtmlContent = result.Value! });
    }
}