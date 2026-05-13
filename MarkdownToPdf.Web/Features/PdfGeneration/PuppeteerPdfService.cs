using PuppeteerSharp;

namespace MarkdownToPdf.Web.Features.PdfGeneration;

internal sealed class PuppeteerPdfService : IPdfService
{
    private static IBrowser? _browser;
    private static readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<byte[]> GenerateFromHtmlAsync(string htmlDocument, CancellationToken cancellationToken)
    {
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

        await using var page = await _browser.NewPageAsync();
        await page.SetJavaScriptEnabledAsync(false);
        await page.SetContentAsync(htmlDocument, new NavigationOptions { WaitUntil = [WaitUntilNavigation.Networkidle0] });

        var pdfBytes = await page.PdfDataAsync(new PdfOptions
        {
            PrintBackground = true,
            PreferCSSPageSize = true
        });

        await page.CloseAsync();

        return pdfBytes;
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null && !_browser.IsClosed)
        {
            await _browser.CloseAsync();
            await _browser.DisposeAsync();
        }

        _lock.Dispose();
    }

}