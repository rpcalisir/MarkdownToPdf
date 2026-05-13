using Markdig;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MarkdownToPdf.Web.Shared.Constants;
using MarkdownToPdf.Web.Shared.Core;
using MarkdownToPdf.Web.Features.PdfGeneration.Generate.Templates;

namespace MarkdownToPdf.Web.Features.PdfGeneration.Generate;

public sealed class GeneratePdfHandler(
    HtmlRenderer htmlRenderer,
    IWebHostEnvironment env,
    IPdfService pdfService) // Inject the interface, not the concrete class!
    : IRequestHandler<GeneratePdfCommand, Result<byte[]>>
{
    private static readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();

    private static string? _cachedCss;
    private static readonly SemaphoreSlim _cssLock = new(1, 1);

    public async Task<Result<byte[]>> Handle(GeneratePdfCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Double-Check Locking pattern for thread-safe lazy loading
            if (_cachedCss is null)
            {
                await _cssLock.WaitAsync(cancellationToken);
                try
                {
                    if (_cachedCss is null)
                    {
                        var cssPath = Path.Combine(env.WebRootPath, "css", "pdf-styles.css");
                        _cachedCss = await File.ReadAllTextAsync(cssPath, cancellationToken);
                    }
                }
                finally
                {
                    _cssLock.Release();
                }
            }

            var rawHtmlContent = Markdown.ToHtml(request.MarkdownText!, _pipeline);

            var fullHtmlDocument = await htmlRenderer.Dispatcher.InvokeAsync(async () =>
            {
                var dictionary = new Dictionary<string, object?>
                {
                    { nameof(PdfDocumentTemplate.CssStyles), _cachedCss },
                    { nameof(PdfDocumentTemplate.ParsedHtmlContent), rawHtmlContent }
                };
                var parameters = ParameterView.FromDictionary(dictionary);
                var output = await htmlRenderer.RenderComponentAsync<PdfDocumentTemplate>(parameters);

                return output.ToHtmlString();
            });

            // Delegate the heavy lifting to the Infrastructure service
            var pdfBytes = await pdfService.GenerateFromHtmlAsync(fullHtmlDocument, cancellationToken);

            return Result<byte[]>.Success(pdfBytes);
        }
        catch (Exception)
        {
            return Result<byte[]>.Failure(DomainErrors.PdfGeneration.FailedToGenerate);
        }
    }
}