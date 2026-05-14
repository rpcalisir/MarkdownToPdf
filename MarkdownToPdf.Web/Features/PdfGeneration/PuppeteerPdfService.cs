using PuppeteerSharp;

namespace MarkdownToPdf.Web.Features.PdfGeneration;

/// <summary>
/// Important Infrastructure Note: Since you removed the --no-sandbox flags, if you are running this application inside a Docker container,
/// it will crash if the container runs as root (which is the Docker default). You will need to ensure your Dockerfile creates a dedicated
/// non-root user and installs the required Chromium dependencies (like libnss3 and libatk-bridge2.0-0) for the sandbox to function properly.
/// </summary>
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
                        // SECURITY FIX: Removed --no-sandbox flags to prevent container escape vulnerabilities.
                        // Ensure your Docker container runs under a non-root user account (e.g., 'pwuser').
                        Args = ["--disable-javascript"]
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