using Ipm.Application.Abstractions.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ipm.Infrastructure.Persistence;

/// <summary>Spec 09 R5-f: reads the settings at start and every 30 seconds so the time zone is applied everywhere, even on an instance that did not take the change.</summary>
internal sealed partial class SettingsRefresher(IServiceScopeFactory scopes, ILogger<SettingsRefresher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IOrganizationSettings>().GetAsync(stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                LogFailed(logger, e);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reading the organization settings failed")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
