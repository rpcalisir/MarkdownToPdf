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
    // PERFORMANCE LEVEL 1: Static Parsing Engine
    private static readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();

    // PERFORMANCE LEVEL 2: In-Memory File Caching to prevent repetitive Disk I/O
    private static string? _cachedCss;

    // PERFORMANCE LEVEL 3: Singleton Browser Instance to prevent slow OS process boot-ups
    private static IBrowser? _browser;
    private static readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<Result<byte[]>> Handle(GeneratePdfCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // 1. Load CSS from Memory (Only hits the hard drive on the very first request)
            if (_cachedCss is null)
            {
                var cssPath = Path.Combine(env.WebRootPath, "css", "pdf-styles.css");
                _cachedCss = await File.ReadAllTextAsync(cssPath, cancellationToken);
            }

            // 2. Parse Markdown instantly using the static pipeline
            var rawHtmlContent = Markdown.ToHtml(request.MarkdownText, _pipeline);

            // 3. Render HTML
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

            // 4. Manage the Singleton Browser safely
            if (_browser is null || _browser.IsClosed)
            {
                await _lock.WaitAsync(cancellationToken);
                try
                {
                    if (_browser is null || _browser.IsClosed)
                    {
                        var browserFetcher = new BrowserFetcher();
                        await browserFetcher.DownloadAsync();

                        _browser = await Puppeteer.LaunchAsync(new LaunchOptions
                        {
                            Headless = true,
                            Args = ["--no-sandbox", "--disable-setuid-sandbox", "--disable-javascript"]
                        });
                    }
                }
                finally
                {
                    _lock.Release();
                }
            }

            // 5. Open a lightweight page tab instead of a whole new browser
            await using var page = await _browser.NewPageAsync();
            await page.SetJavaScriptEnabledAsync(false);

            await page.SetContentAsync(fullHtmlDocument, new PuppeteerSharp.NavigationOptions { WaitUntil = [WaitUntilNavigation.Networkidle0] });

            var pdfBytes = await page.PdfDataAsync(new PdfOptions
            {
                PrintBackground = true,
                PreferCSSPageSize = true
            });

            // Clean up the tab to free memory, leaving the main browser process alive for the next user
            await page.CloseAsync();

            return Result<byte[]>.Success(pdfBytes);
        }
        catch (Exception)
        {
            return Result<byte[]>.Failure(DomainErrors.PdfGeneration.FailedToGenerate);
        }
    }
}