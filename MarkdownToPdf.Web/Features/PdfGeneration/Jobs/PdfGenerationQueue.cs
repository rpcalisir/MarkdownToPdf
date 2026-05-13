using System.Threading.Channels;

namespace MarkdownToPdf.Web.Features.PdfGeneration.Jobs;

internal sealed class PdfGenerationQueue : IPdfGenerationQueue
{
    private readonly Channel<PdfGenerationJob> _queue;

    public PdfGenerationQueue()
    {
        var options = new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait
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
