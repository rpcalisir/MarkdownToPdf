using System.Threading.Channels;

namespace MarkdownToPdf.Web.Features.PdfGeneration.Jobs;

internal sealed class PdfGenerationQueue : IPdfGenerationQueue
{
    private readonly Channel<PdfGenerationJob> _queue;

    public PdfGenerationQueue()
    {
        // PERFORMANCE FIX: Changed FullMode from Wait to DropWrite. 
        // If the queue exceeds capacity during a traffic spike, DropWrite instantly discards 
        // the new job rather than blocking the Minimal API thread, completely preventing thread pool starvation.
        var options = new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropWrite
        };

        _queue = Channel.CreateBounded<PdfGenerationJob>(options);
    }

    public async ValueTask QueueJobAsync(PdfGenerationJob job, CancellationToken cancellationToken)
    {
        await _queue.Writer.WriteAsync(job, cancellationToken);
    }

    public IAsyncEnumerable<PdfGenerationJob> ReadAllAsync(CancellationToken cancellationToken)
    {
        return _queue.Reader.ReadAllAsync(cancellationToken);
    }
}