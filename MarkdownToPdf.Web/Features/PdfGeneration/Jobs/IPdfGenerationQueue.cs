namespace MarkdownToPdf.Web.Features.PdfGeneration.Jobs;

public interface IPdfGenerationQueue
{
    ValueTask QueueJobAsync(PdfGenerationJob job, CancellationToken cancellationToken);
    IAsyncEnumerable<PdfGenerationJob> ReadAllAsync(CancellationToken cancellationToken);
}
