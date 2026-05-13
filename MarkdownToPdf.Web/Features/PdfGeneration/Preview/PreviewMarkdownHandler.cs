using Markdig;
using MediatR;
using MarkdownToPdf.Web.Shared.Core;

namespace MarkdownToPdf.Web.Features.PdfGeneration.Preview;

internal sealed class PreviewMarkdownHandler : IRequestHandler<PreviewMarkdownCommand, Result<string>>
{
    public Task<Result<string>> Handle(PreviewMarkdownCommand request, CancellationToken cancellationToken)
    {
        // If the user clears the editor, simply return an empty string to clear the preview pane
        if (string.IsNullOrWhiteSpace(request.MarkdownText))
        {
            return Task.FromResult(Result<string>.Success(string.Empty));
        }

        // Parse markdown strictly securely for the live preview
        var pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .DisableHtml()
            .Build();

        var html = Markdown.ToHtml(request.MarkdownText, pipeline);

        return Task.FromResult(Result<string>.Success(html));
    }
}