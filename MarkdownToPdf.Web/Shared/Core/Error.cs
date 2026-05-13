namespace MarkdownToPdf.Web.Shared.Core;

// The base Error record for standard business failures
public record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);
}