using Markdig;
using MediatR;
using MarkdownToPdf.Web.Shared.Core;

namespace MarkdownToPdf.Web.Features.PdfGeneration.Preview;

internal sealed class PreviewMarkdownHandler : IRequestHandler<PreviewMarkdownCommand, Result<string>>
{
    // PERFORMANCE: Compile the heavy parsing engine exactly once per application lifecycle.
    // By making this static, we eliminate massive object allocation overhead on every keystroke.
    private static readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();

    public Task<Result<string>> Handle(PreviewMarkdownCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.MarkdownText))
        {
            return Task.FromResult(Result<string>.Success(string.Empty));
        }

        // Use the pre-compiled static pipeline
        var html = Markdown.ToHtml(request.MarkdownText, _pipeline);

        return Task.FromResult(Result<string>.Success(html));
    }
}