using MediatR;
using MarkdownToPdf.Web.Shared.Core;

namespace MarkdownToPdf.Web.Features.PdfGeneration.Generate;

public sealed record GeneratePdfCommand(
    string MarkdownText
) : IRequest<Result<byte[]>>;