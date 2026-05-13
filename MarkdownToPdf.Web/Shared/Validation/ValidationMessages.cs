namespace MarkdownToPdf.Web.Shared.Validation;

public static class ValidationMessages
{
    public const string RequiredField = "{PropertyName} is required.";
    public const string InvalidEmail = "Please enter a valid email address.";
    public const string MaxLength = "{PropertyName} cannot exceed {MaxLength} characters.";
    public const string MinLength = "{PropertyName} must be at least {MinLength} characters long.";
    public const string RequiresUppercase = "{PropertyName} must contain at least one uppercase letter.";
    public const string RequiresLowercase = "{PropertyName} must contain at least one lowercase letter.";
    public const string RequiresDigit = "{PropertyName} must contain at least one number.";
    public const string RequiresNonAlphanumeric = "{PropertyName} must contain at least one non alphanumeric number.";
}