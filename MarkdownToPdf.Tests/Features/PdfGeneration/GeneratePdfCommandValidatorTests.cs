using FluentValidation.TestHelper;
using MarkdownToPdf.Web.Features.PdfGeneration.Generate;
using Xunit;

namespace MarkdownToPdf.Tests.Features.PdfGeneration;

public sealed class GeneratePdfCommandValidatorTests
{
    private readonly GeneratePdfCommandValidator _validator = new();

    [Fact]
    public void Validator_ShouldHaveError_WhenMarkdownIsEmpty()
    {
        // Arrange
        var command = new GeneratePdfCommand(string.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert: Ensures users cannot spam empty requests to the Puppeteer engine
        result.ShouldHaveValidationErrorFor(x => x.MarkdownText)
              .WithErrorMessage("Oops! Please type or paste some Markdown before generating your PDF.");
    }

    [Fact]
    public void Validator_ShouldHaveError_WhenMarkdownIsOnlyWhitespace()
    {
        // Arrange
        var command = new GeneratePdfCommand("   \n \t  ");

        // Act
        var result = _validator.TestValidate(command);

        // Assert: Catches invisible characters that bypass standard empty checks
        result.ShouldHaveValidationErrorFor(x => x.MarkdownText);
    }

    [Fact]
    public void Validator_ShouldHaveError_WhenMarkdownExceedsLimit()
    {
        // Arrange: Simulate a malicious user pasting a massive payload to exhaust RAM
        var massivePayload = new string('A', 50001);
        var command = new GeneratePdfCommand(massivePayload);

        // Act
        var result = _validator.TestValidate(command);

        // Assert: Verifies our 50k hard limit holds up
        result.ShouldHaveValidationErrorFor(x => x.MarkdownText)
              .WithErrorMessage("Your Markdown is a bit too long! Please keep it under 50,000 characters.");
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenMarkdownTextIsEmpty()
    {
        // Arrange
        // ARCHITECTURAL FIX: Instantiating the record using its primary constructor 
        // instead of an object initializer to satisfy the compiler.
        var command = new GeneratePdfCommand(string.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        // Ensures the test assertions perfectly match our updated, user-friendly UI contract.
        // TEST FIX: Synchronized the expected error message with the actual updated validator output.
        result.ShouldHaveValidationErrorFor(x => x.MarkdownText)
              .WithErrorMessage("Oops! Please type or paste some Markdown before generating your PDF.");
    }
}