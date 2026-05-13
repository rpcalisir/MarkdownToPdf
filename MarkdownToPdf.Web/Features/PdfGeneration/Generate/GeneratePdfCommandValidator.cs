using FluentValidation;

namespace MarkdownToPdf.Web.Features.PdfGeneration.Generate;

public sealed class GeneratePdfCommandValidator : AbstractValidator<GeneratePdfCommand>
{
    public GeneratePdfCommandValidator()
    {
        RuleFor(x => x.MarkdownText)
            .Cascade(CascadeMode.Stop) // Stops validating if the first rule fails so we don't get duplicate errors
            .NotEmpty().WithMessage("Oops! Please type or paste some Markdown before generating your PDF.")
            .Must(text => !string.IsNullOrWhiteSpace(text)).WithMessage("Oops! Please type or paste some Markdown before generating your PDF.")
            .MaximumLength(50000).WithMessage("Your Markdown is a bit too long! Please keep it under 50,000 characters.");
    }
}