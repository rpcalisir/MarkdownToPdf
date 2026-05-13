using Markdig;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MarkdownToPdf.Web.Shared.Constants;
using MarkdownToPdf.Web.Shared.Core;
using MarkdownToPdf.Web.Features.PdfGeneration.Generate.Templates;
using PuppeteerSharp;

namespace MarkdownToPdf.Web.Features.PdfGeneration.Generate;

internal sealed class GeneratePdfHandler(
    HtmlRenderer htmlRenderer,
    IWebHostEnvironment env)
    : IRequestHandler<GeneratePdfCommand, Result<byte[]>>
{
    // PERFORMANCE: Ensures the heavy browser download only executes once per application lifecycle
    private static bool _isBrowserDownloaded = false;
    private static readonly SemaphoreSlim _browserLock = new(1, 1);

    public async Task<Result<byte[]>> Handle(GeneratePdfCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var cssPath = Path.Combine(env.WebRootPath, "css", "pdf-styles.css");
            var cssContent = await File.ReadAllTextAsync(cssPath, cancellationToken);

            // SECURITY: DisableHtml() prevents users from injecting raw <script> or <iframe> tags via markdown
            var pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .DisableHtml()
                .Build();

            var rawHtmlContent = Markdown.ToHtml(request.MarkdownText, pipeline);

            var fullHtmlDocument = await htmlRenderer.Dispatcher.InvokeAsync(async () =>
            {
                var dictionary = new Dictionary<string, object?>
                {
                    { nameof(PdfDocumentTemplate.CssStyles), cssContent },
                    { nameof(PdfDocumentTemplate.ParsedHtmlContent), rawHtmlContent }
                };
                var parameters = ParameterView.FromDictionary(dictionary);
                var output = await htmlRenderer.RenderComponentAsync<PdfDocumentTemplate>(parameters);

                return output.ToHtmlString();
            });

            // Thread-safe check to initialize Puppeteer resources
            if (!_isBrowserDownloaded)
            {
                await _browserLock.WaitAsync(cancellationToken);
                try
                {
                    if (!_isBrowserDownloaded)
                    {
                        var browserFetcher = new BrowserFetcher();
                        await browserFetcher.DownloadAsync();
                        _isBrowserDownloaded = true;
                    }
                }
                finally
                {
                    _browserLock.Release();
                }
            }

            // SECURITY: Disable JavaScript execution inside the Chromium instance to prevent Server-Side Request Forgery
            await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                Args = ["--no-sandbox", "--disable-setuid-sandbox", "--disable-javascript"]
            });

            await using var page = await browser.NewPageAsync();
            await page.SetJavaScriptEnabledAsync(false);

            await page.SetContentAsync(fullHtmlDocument, new PuppeteerSharp.NavigationOptions { WaitUntil = [WaitUntilNavigation.Networkidle0] });

            var pdfBytes = await page.PdfDataAsync(new PdfOptions
            {
                PrintBackground = true,
                PreferCSSPageSize = true
            });

            return Result<byte[]>.Success(pdfBytes);
        }
        catch (Exception)
        {
            return Result<byte[]>.Failure(DomainErrors.PdfGeneration.FailedToGenerate);
        }
    }
}