using Ipm.Application.Abstractions.Telegram;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ipm.Infrastructure.Telegram;

public static class TelegramServiceCollectionExtensions
{
    /// <summary>
    /// Spec 11 R1. The options, the settings, the link tokens and the bot client are always registered (the application depends on them), but the dispatcher
    /// and the update loop only run when Telegram is on with a token, so a missing token degrades to "off" instead of failing the start.
    /// </summary>
    public static IServiceCollection AddTelegram(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<TelegramOptions>()
            .Bind(configuration.GetSection(TelegramOptions.Section))
            .ValidateDataAnnotations()
            .Validate(o => o.Problem() is null, "The Telegram settings are not complete (see Telegram:BotUsername, WebhookUrl and WebhookSecret).")
            .ValidateOnStart();

        services.AddSingleton<ITelegramSettings, TelegramSettings>();
        services.AddScoped<ITelegramLinkTokens, RedisTelegramLinkTokens>();

        // The token is in every URL (/bot<token>/method): no HTTP logger may see this client, or it would reach the log server.
        services.AddHttpClient<ITelegramBot, TelegramBotClient>((sp, client) =>
        {
            var token = sp.GetRequiredService<IOptions<TelegramOptions>>().Value.BotToken.Trim();
            client.BaseAddress = new Uri($"https://api.telegram.org/bot{token}/");
            client.Timeout = TimeSpan.FromSeconds(30);
        }).RemoveAllLoggers();

        var options = configuration.GetSection(TelegramOptions.Section).Get<TelegramOptions>() ?? new TelegramOptions();
        services.AddSingleton<TelegramOutboxDispatcher>();
        if (options.IsActive && options.BackgroundServices)
        {
            services.AddHostedService<TelegramOutboxWorker>();
            services.AddHostedService<TelegramUpdatesPoller>();
        }

        return services;
    }
}
