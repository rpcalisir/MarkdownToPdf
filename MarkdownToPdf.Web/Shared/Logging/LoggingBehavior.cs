using MediatR;
using MarkdownToPdf.Web.Shared.Constants;
using MarkdownToPdf.Web.Shared.Core;
using System.Diagnostics;

namespace MarkdownToPdf.Web.Shared.Logging;

public sealed partial class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        // Push the request name into the log context so all subsequent logs in this slice share the tag
        using var logScope = logger.BeginScope(new Dictionary<string, object>
        {
            [DiagnosticKeys.RequestName] = requestName
        });

        LogExecutingCommand(logger, requestName);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next();
            stopwatch.Stop();

            if (response is Result result && result.IsFailure)
            {
                // The '@' symbol before Error ensures Serilog serializes the Error object into JSON 
                // so we can query specific Error Codes in our log dashboard.
                LogCommandFailed(logger, requestName, stopwatch.ElapsedMilliseconds, result.Error);
            }
            else
            {
                LogCommandSuccess(logger, requestName, stopwatch.ElapsedMilliseconds);
            }

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Ensures critical system exceptions that bypass the Result object pattern are traced 
            // back to the specific MediatR command before bubbling up.
            LogCommandError(logger, ex, requestName, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Executing command {RequestName}")]
    private static partial void LogExecutingCommand(ILogger logger, string requestName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Command {RequestName} failed in {ElapsedMilliseconds}ms with error: {@Error}")]
    private static partial void LogCommandFailed(ILogger logger, string requestName, long elapsedMilliseconds, Error error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Command {RequestName} completed successfully in {ElapsedMilliseconds}ms")]
    private static partial void LogCommandSuccess(ILogger logger, string requestName, long elapsedMilliseconds);

    [LoggerMessage(Level = LogLevel.Error, Message = "Command {RequestName} threw an unhandled exception after {ElapsedMilliseconds}ms")]
    private static partial void LogCommandError(ILogger logger, Exception exception, string requestName, long elapsedMilliseconds);
}