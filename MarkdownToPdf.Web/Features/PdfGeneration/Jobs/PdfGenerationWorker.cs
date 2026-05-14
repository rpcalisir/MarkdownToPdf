using Markdig;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Caching.Distributed;
using MarkdownToPdf.Web.Features.PdfGeneration.Generate.Templates;
using System.Text.Json;

namespace MarkdownToPdf.Web.Features.PdfGeneration.Jobs;

public sealed class PdfGenerationWorker : BackgroundService
{
    private readonly IPdfGenerationQueue _queue;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IDistributedCache _cache;
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
        IDistributedCache cache,
        ILogger<PdfGenerationWorker> logger)
    {
        _queue = queue;
        _serviceScopeFactory = serviceScopeFactory;
        _cache = cache;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // PERFORMANCE FIX: Bounded parallelism prevents a single slow PDF from blocking the queue.
        // It simultaneously protects the host machine by restricting concurrent browser pages to logical cores.
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount > 0 ? Environment.ProcessorCount : 2,
            CancellationToken = stoppingToken
        };

        await Parallel.ForEachAsync(_queue.ReadAllAsync(stoppingToken), parallelOptions, async (job, token) =>
        {
            try
            {
                await UpdateJobStateAsync(job.JobId, JobStatus.Processing, token: token);

                // ENTERPRISE FIX: Use CreateAsyncScope() instead of CreateScope().
                // HtmlRenderer implements IAsyncDisposable. Ensuring it is disposed asynchronously 
                // prevents silent thread-blocking faults in background services.
                await using var scope = _serviceScopeFactory.CreateAsyncScope();

                var htmlRenderer = scope.ServiceProvider.GetRequiredService<HtmlRenderer>();
                var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
                var pdfService = scope.ServiceProvider.GetRequiredService<IPdfService>();

                if (_cachedCss is null)
                {
                    await _cssLock.WaitAsync(token);
                    try
                    {
                        if (_cachedCss is null)
                        {
                            var cssPath = Path.Combine(env.WebRootPath, "css", "pdf-styles.css");
                            _cachedCss = await File.ReadAllTextAsync(cssPath, token);
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
                var pdfBytes = await pdfService.GenerateFromHtmlAsync(fullHtmlDocument, token);

                await UpdateJobStateAsync(job.JobId, JobStatus.Completed, pdfBytes, token: token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process PDF job {JobId}", job.JobId);
                await UpdateJobStateAsync(job.JobId, JobStatus.Failed, errorMessage: "An error occurred while generating the document.", token: token);
            }
        });
    }

    private async Task UpdateJobStateAsync(Guid jobId, JobStatus status, byte[]? pdfBytes = null, string? errorMessage = null, CancellationToken token = default)
    {
        var state = new PdfJobState(jobId, status, pdfBytes, errorMessage);
        var stateJson = JsonSerializer.Serialize(state);

        // ARCHITECTURAL NOTE: For multi-node deployments, IDistributedCache ensures 
        // HTMX status polls can hit any server instance behind the load balancer.
        await _cache.SetStringAsync(
            jobId.ToString(),
            stateJson,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) },
            token);
    }
}