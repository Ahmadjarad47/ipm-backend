using System.Diagnostics;
using Ipm.Domain.Common;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Ipm.Application.Behaviors;

/// <summary>Logs every command/query with its duration, and business failures as warnings.</summary>
public sealed partial class LoggingBehavior<TMessage, TResponse>(ILogger<LoggingBehavior<TMessage, TResponse>> logger)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
    where TResponse : Result
{
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        var name = typeof(TMessage).Name;
        var started = Stopwatch.GetTimestamp();

        var response = await next(message, cancellationToken);

        var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        if (response.IsSuccess)
        {
            LogCompleted(logger, name, elapsedMs);
        }
        else
        {
            LogFailed(logger, name, response.Error.Code, elapsedMs);
        }

        return response;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Message} completed in {ElapsedMs:0.0} ms")]
    private static partial void LogCompleted(ILogger logger, string message, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Message} failed with {ErrorCode} in {ElapsedMs:0.0} ms")]
    private static partial void LogFailed(ILogger logger, string message, string errorCode, double elapsedMs);
}
