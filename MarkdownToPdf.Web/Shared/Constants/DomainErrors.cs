using MarkdownToPdf.Web.Shared.Core;

namespace MarkdownToPdf.Web.Shared.Constants;

public static class DomainErrors
{
    // Group errors by concept using nested static classes
    public static class Validation
    {
        public static readonly Error Failure = new(
            "Validation.Failure",
            "One or more validation errors occurred.");
    }

    public static class Authentication
    {
        public static readonly Error DuplicateEmail = new(
            "Authentication.DuplicateEmail",
            "The provided email address is already in use.");
    }

    public static class PdfGeneration
    {
        public static readonly Core.Error FailedToGenerate = new(
            "PdfGeneration.Failed",
            "An unexpected error occurred while generating the PDF document.");
    }
}