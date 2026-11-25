namespace Ipm.Application.Abstractions.Telegram;

/// <summary>What the application needs to know about the Telegram setup (spec 11 R1). Implemented by the infrastructure from the <c>Telegram</c> options.</summary>
public interface ITelegramSettings
{
    /// <summary>On, with a bot token. Off means no background work and no link endpoint.</summary>
    bool Enabled { get; }

    /// <summary>The bot name without the at sign, as in <c>https://t.me/&lt;name&gt;</c>.</summary>
    string BotUsername { get; }

    /// <summary>Where the app lives, to turn a relative notification link into an absolute one. Empty when not set.</summary>
    string AppBaseUrl { get; }

    /// <summary>The secret Telegram sends back in <c>X-Telegram-Bot-Api-Secret-Token</c>.</summary>
    string WebhookSecret { get; }
}
