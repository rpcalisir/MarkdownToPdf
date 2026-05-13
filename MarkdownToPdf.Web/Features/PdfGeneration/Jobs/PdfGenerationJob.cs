namespace MarkdownToPdf.Web.Features.PdfGeneration.Jobs;

public sealed record PdfGenerationJob(Guid JobId, string MarkdownText);
