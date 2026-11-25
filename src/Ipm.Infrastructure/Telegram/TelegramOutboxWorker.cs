using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ipm.Infrastructure.Telegram;

/// <summary>Spec 11 R8: runs <see cref="TelegramOutboxDispatcher"/> every second for as long as the application lives. A failed pass is logged and tried again.</summary>
internal sealed partial class TelegramOutboxWorker(TelegramOutboxDispatcher dispatcher, ILogger<TelegramOutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await WaitAsync(timer, stoppingToken))
        {
            try
            {
                await dispatcher.DispatchOnceAsync(stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                LogFailed(logger, e);
            }
        }
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sending the Telegram outbox failed; trying again in a second")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
