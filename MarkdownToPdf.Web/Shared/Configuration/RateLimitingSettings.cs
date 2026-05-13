namespace MarkdownToPdf.Web.Shared.Configuration;

/// <summary>
/// Strongly typed configuration model for rate limiting policies.
/// </summary>
public sealed class RateLimitingSettings
{
    public const string SectionName = "RateLimiting:PdfGeneration";
    public int PermitLimit { get; init; } = 3;
    public int WindowSeconds { get; init; } = 60;
}