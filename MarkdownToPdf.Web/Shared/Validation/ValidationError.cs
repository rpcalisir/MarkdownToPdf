using MarkdownToPdf.Web.Shared.Constants;
using MarkdownToPdf.Web.Shared.Core;

namespace MarkdownToPdf.Web.Shared.Validation;

// The specialized Validation Error that carries an array of sub-errors.
// It inherits from the base Error so it can still fit inside your Result object.
public sealed record ValidationError(Error[] Errors)
    : Error(DomainErrors.Validation.Failure.Code, DomainErrors.Validation.Failure.Message);