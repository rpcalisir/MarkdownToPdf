using MarkdownToPdf.Web.Features.PdfGeneration;

namespace MarkdownToPdf.Tests.Features.PdfGeneration;

public sealed class FakePdfService : IPdfService
{
    public Task<byte[]> GenerateFromHtmlAsync(string htmlDocument, CancellationToken cancellationToken)
    {
        // Instantly return a dummy PDF byte array
        return Task.FromResult(new byte[] { 1, 2, 3, 4, 5 });
    }
}
