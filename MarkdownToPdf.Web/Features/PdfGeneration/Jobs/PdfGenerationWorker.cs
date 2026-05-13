using Markdig;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Caching.Memory;
using MarkdownToPdf.Web.Features.PdfGeneration.Generate.Templates;

namespace MarkdownToPdf.Web.Features.PdfGeneration.Jobs;

public sealed class PdfGenerationWorker : BackgroundService
{
    private readonly IPdfGenerationQueue _queue;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PdfGenerationWorker> _logger;

    private static readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();

    private static string? _cachedCss;
    private static readonly SemaphoreSlim _cssLock = new(1, 1);

    public PdfGenerationWorker(
        IPdfGenerationQueue queue,
        IServiceScopeFactory serviceScopeFactory,
        IMemoryCache cache,
        ILogger<PdfGenerationWorker> logger)
    {
        _queue = queue;
        _serviceScopeFactory = serviceScopeFactory;
        _cache = cache;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Continuously read from the channel as jobs arrive
        await foreach (var job in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                UpdateJobState(job.JobId, JobStatus.Processing);

                // ENTERPRISE FIX: Use CreateAsyncScope() instead of CreateScope().
                // HtmlRenderer implements IAsyncDisposable. Ensuring it is disposed asynchronously 
                // prevents silent thread-blocking faults in background services.
                await using var scope = _serviceScopeFactory.CreateAsyncScope();

                var htmlRenderer = scope.ServiceProvider.GetRequiredService<HtmlRenderer>();
                var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
                var pdfService = scope.ServiceProvider.GetRequiredService<IPdfService>();

                if (_cachedCss is null)
                {
                    await _cssLock.WaitAsync(stoppingToken);
                    try
                    {
                        if (_cachedCss is null)
                        {
                            var cssPath = Path.Combine(env.WebRootPath, "css", "pdf-styles.css");
                            _cachedCss = await File.ReadAllTextAsync(cssPath, stoppingToken);
                        }
                    }
                    finally
                    {
                        _cssLock.Release();
                    }
                }

                var rawHtmlContent = Markdown.ToHtml(job.MarkdownText, _pipeline);

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

                // Delegate the heavy OS process to the infrastructure service
                var pdfBytes = await pdfService.GenerateFromHtmlAsync(fullHtmlDocument, stoppingToken);

                UpdateJobState(job.JobId, JobStatus.Completed, pdfBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process PDF job {JobId}", job.JobId);
                UpdateJobState(job.JobId, JobStatus.Failed, errorMessage: "An error occurred while generating the document.");
            }
        }
    }

    private void UpdateJobState(Guid jobId, JobStatus status, byte[]? pdfBytes = null, string? errorMessage = null)
    {
        var state = new PdfJobState(jobId, status, pdfBytes, errorMessage);
        _cache.Set(jobId, state, TimeSpan.FromMinutes(5));
    }
}