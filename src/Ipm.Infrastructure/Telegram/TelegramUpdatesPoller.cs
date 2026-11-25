using Ipm.Application.Abstractions.Telegram;
using Ipm.Application.Features.Activity.Telegram.Commands.ProcessTelegramUpdate;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ipm.Infrastructure.Telegram;

/// <summary>
/// Spec 11 R10: how Telegram messages reach the system. In <see cref="TelegramMode.Polling"/> it removes any webhook once and then long-polls
/// <c>getUpdates</c>; in <see cref="TelegramMode.Webhook"/> it only registers the webhook (with the secret) and the endpoint does the rest.
/// Each update goes through <see cref="ProcessTelegramUpdateCommand"/> in a scope of its own.
/// </summary>
internal sealed partial class TelegramUpdatesPoller(IServiceScopeFactory scopes, IOptions<TelegramOptions> options, ILogger<TelegramUpdatesPoller> logger) : BackgroundService
{
    private const int LongPollSeconds = 25;
    private static readonly TimeSpan MaxPause = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            if (options.Value.Mode == TelegramMode.Webhook)
            {
                await RegisterWebhookAsync(stoppingToken);
            }
            else
            {
                await PollAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }

    private async Task RegisterWebhookAsync(CancellationToken cancellationToken)
    {
        var pause = TimeSpan.FromSeconds(2);
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var scope = scopes.CreateAsyncScope();
            var bot = scope.ServiceProvider.GetRequiredService<ITelegramBot>();
            var result = await bot.SetWebhookAsync(options.Value.WebhookUrl, options.Value.WebhookSecret, cancellationToken);
            if (result.IsSuccess)
            {
                LogWebhookSet(logger);
                return;
            }

            LogCallFailed(logger, result.Error.Code, pause.TotalSeconds);
            await Task.Delay(pause, cancellationToken);
            pause = Next(pause);
        }
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        var pause = TimeSpan.FromSeconds(2);
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var scope = scopes.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<ITelegramBot>().DeleteWebhookAsync(cancellationToken);
            if (result.IsSuccess)
            {
                break;
            }

            LogCallFailed(logger, result.Error.Code, pause.TotalSeconds);
            await Task.Delay(pause, cancellationToken);
            pause = Next(pause);
        }

        LogPolling(logger);
        long offset = 0;
        pause = TimeSpan.FromSeconds(2);
        while (!cancellationToken.IsCancellationRequested)
        {
            var updates = await GetUpdatesAsync(offset, cancellationToken);
            if (updates.IsFailure)
            {
                LogCallFailed(logger, updates.Error.Code, pause.TotalSeconds);
                await Task.Delay(pause, cancellationToken);
                pause = Next(pause);
                continue;
            }

            pause = TimeSpan.FromSeconds(2);
            foreach (var update in updates.Value)
            {
                offset = Math.Max(offset, update.UpdateId + 1);
                await ProcessAsync(update, cancellationToken);
            }
        }
    }

    private async Task<Ipm.Domain.Common.Result<IReadOnlyList<TelegramUpdate>>> GetUpdatesAsync(long offset, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ITelegramBot>().GetUpdatesAsync(offset, LongPollSeconds, cancellationToken);
    }

    private async Task ProcessAsync(TelegramUpdate update, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ProcessTelegramUpdateCommand(update), cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            LogUpdateFailed(logger, update.UpdateId, e);
        }
    }

    private static TimeSpan Next(TimeSpan pause) => TimeSpan.FromTicks(Math.Min(pause.Ticks * 2, MaxPause.Ticks));

    [LoggerMessage(Level = LogLevel.Information, Message = "Telegram webhook registered")]
    private static partial void LogWebhookSet(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Telegram long polling started")]
    private static partial void LogPolling(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Telegram call failed ({Code}); trying again in {Seconds} s")]
    private static partial void LogCallFailed(ILogger logger, string code, double seconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Handling Telegram update {UpdateId} failed")]
    private static partial void LogUpdateFailed(ILogger logger, long updateId, Exception exception);
}
