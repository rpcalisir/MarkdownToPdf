namespace MarkdownToPdf.Web.Features.PdfGeneration;

public interface IPdfService
{
    Task<byte[]> GenerateFromHtmlAsync(string htmlDocument, CancellationToken cancellationToken);
}