namespace MarkdownToPdf.Web.Features.PdfGeneration.Jobs;

public sealed class PdfJobState
{
    public Guid JobId { get; }
    public JobStatus Status { get; }
    public byte[]? PdfBytes { get; }
    public string? ErrorMessage { get; }

    public PdfJobState(Guid jobId, JobStatus status, byte[]? pdfBytes = null, string? errorMessage = null)
    {
        JobId = jobId;
        Status = status;
        PdfBytes = pdfBytes;
        ErrorMessage = errorMessage;
    }
}