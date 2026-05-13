using FluentValidation;
using MediatR;
using MarkdownToPdf.Web.Shared.Core;
using System.Reflection;

namespace MarkdownToPdf.Web.Shared.Validation;

/// <summary>
/// Intercepts all MediatR requests to run FluentValidation rules.
/// Safely handles both generic Result<T> and non-generic Result returns without throwing casting exceptions.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        var validationFailures = await Task.WhenAll(
            validators.Select(validator => validator.ValidateAsync(context, cancellationToken)));

        var errors = validationFailures
            .Where(validationResult => !validationResult.IsValid)
            .SelectMany(validationResult => validationResult.Errors)
            .ToList();

        if (errors.Count != 0)
        {
            var errorArray = errors
                .Select(failure => new Error(failure.PropertyName, failure.ErrorMessage))
                .ToArray();

            var validationError = new ValidationError(errorArray);

            // If the endpoint just expects a standard success/fail Result (like RegisterCommand)
            if (typeof(TResponse) == typeof(Result))
            {
                return (TResponse)(object)Result.Failure(validationError);
            }

            // If the endpoint expects data back, like Result<byte[]>
            if (typeof(TResponse).IsGenericType && typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
            {
                var resultType = typeof(TResponse).GetGenericArguments()[0];
                var genericResultType = typeof(Result<>).MakeGenericType(resultType);

                // Safely invokes the private constructor of Result<T> using Activator to avoid InvalidCastExceptions
                var failureInstance = Activator.CreateInstance(
                    genericResultType,
                    BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    [false, validationError, null],
                    null);

                return (TResponse)failureInstance!;
            }
        }

        return await next();
    }
}