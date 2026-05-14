namespace MarkdownToPdf.Web.Features.PdfGeneration.Jobs;

using System.Threading.Channels;

internal sealed class PdfGenerationQueue : IPdfGenerationQueue
{
    private readonly Channel<PdfGenerationJob> _queue;

    public PdfGenerationQueue()
    {
        // PERFORMANCE & RELIABILITY FIX: Reverted from DropWrite to Wait.
        // We handle the bounds-checking manually to fail-fast and reject the request 
        // gracefully rather than silently dropping data while the client polls forever.
        var options = new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait
        };

        _queue = Channel.CreateBounded<PdfGenerationJob>(options);
    }

    public ValueTask QueueJobAsync(PdfGenerationJob job, CancellationToken cancellationToken)
    {
        // Load Shedding: If the system is saturated, TryWrite returns false immediately.
        // We throw an exception which will be caught by the MediatR pipeline or global error handler
        // to return a failure result to the UI.
        if (!_queue.Writer.TryWrite(job))
        {
            throw new InvalidOperationException("The document processing queue is currently at maximum capacity. Please try again later.");
        }

        return ValueTask.CompletedTask;
    }

    public IAsyncEnumerable<PdfGenerationJob> ReadAllAsync(CancellationToken cancellationToken)
    {
        return _queue.Reader.ReadAllAsync(cancellationToken);
    }
}