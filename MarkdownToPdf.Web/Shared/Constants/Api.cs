namespace MarkdownToPdf.Web.Shared.Constants;

public static class Api
{
    public static class Routes
    {
        private const string Base = "/api";

        public static class Authentication
        {
            public const string Prefix = $"{Base}/auth";

            // Absolute paths shared across the entire stack
            public const string Register = $"{Prefix}/register";
        }

        public static class PdfGeneration
        {
            public const string Prefix = $"{Base}/pdf";
            public const string Generate = $"{Prefix}/generate";
        }
    }

    public static class Tags
    {
        public const string Authentication = "Authentication";
        public const string PdfGeneration = "PdfGeneration";
    }

    public static class Messages
    {
        public const string RegistrationSuccess = "User registered successfully.";
        public const string PdfGeneratedSuccess = "PDF generated successfully. Click the button to download.";
    }
}