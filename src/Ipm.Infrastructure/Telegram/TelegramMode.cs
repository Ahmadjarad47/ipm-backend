namespace Ipm.Infrastructure.Telegram;

/// <summary>How updates reach the system (spec 11 R10).</summary>
public enum TelegramMode
{
    /// <summary>Long polling with getUpdates: no public address needed (development).</summary>
    Polling = 0,

    /// <summary>Telegram calls <c>POST /api/v1/telegram/webhook</c> (production).</summary>
    Webhook = 1,
}
