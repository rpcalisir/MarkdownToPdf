using FluentValidation;
using MarkdownToPdf.Web.Shared.Constants;

namespace MarkdownToPdf.Web.Shared.Validation;

public static class ValidationExtensions
{
    // ==============================================================================
    // PART 1: RULE DEFINITIONS (The "What")
    // Extends IRuleBuilder to define reusable format rules across the application.
    // ==============================================================================

    /// <summary>
    /// Enforces standard enterprise email formatting rules.
    /// </summary>
    public static IRuleBuilderOptions<T, string> ValidEmailFormat<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .NotEmpty().WithMessage(ValidationMessages.RequiredField)
            .EmailAddress().WithMessage(ValidationMessages.InvalidEmail);
    }

    /// <summary>
    /// Enforces standard enterprise password complexity rules.
    /// Synchronized with ASP.NET Core Identity requirements.
    /// </summary>
    public static IRuleBuilderOptions<T, string> ValidPasswordFormat<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .NotEmpty().WithMessage(ValidationMessages.RequiredField)
            .MinimumLength(8).WithMessage(ValidationMessages.MinLength)
            .Matches("[A-Z]").WithMessage(ValidationMessages.RequiresUppercase)
            .Matches("[a-z]").WithMessage(ValidationMessages.RequiresLowercase)
            .Matches("[0-9]").WithMessage(ValidationMessages.RequiresDigit)
            // REFACTORED: Synchronized with Identity's RequireNonAlphanumeric = true policy
            .Matches("[^a-zA-Z0-9]").WithMessage(ValidationMessages.RequiresNonAlphanumeric);
    }

    // ==============================================================================
    // PART 2: RULE EXECUTION (The "How/When")
    // Extends IValidator to allow surgical, single-property validation for HTMX.
    // ==============================================================================

    /// <summary>
    /// Surgically executes validation for a single property of a command, ignoring the rest.
    /// Used by HTMX partial endpoints to validate fields on the 'blur' event.
    /// </summary>
    public static async Task<string?> ValidateFieldAsync<TCommand>(
        this IValidator<TCommand> validator,
        TCommand command,
        string propertyName,
        CancellationToken cancellationToken = default)
    {
        // Enterprise Guard Clause against malformed/null payloads
        if (command is null)
        {
            return null;
        }

        var result = await validator.ValidateAsync(command, options =>
        {
            // Command FluentValidation to ONLY check this specific property
            options.IncludeProperties(propertyName);
        }, cancellationToken);

        // Return the error message if it exists, otherwise return null
        return result.Errors.FirstOrDefault(e =>
            e.PropertyName.Equals(propertyName, StringComparison.OrdinalIgnoreCase))?.ErrorMessage;
    }
}