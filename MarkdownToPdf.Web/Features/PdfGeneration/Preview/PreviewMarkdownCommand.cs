using MediatR;
using MarkdownToPdf.Web.Shared.Core;

namespace MarkdownToPdf.Web.Features.PdfGeneration.Preview;

public sealed record PreviewMarkdownCommand(
    string? MarkdownText)
    : IRequest<Result<string>>;