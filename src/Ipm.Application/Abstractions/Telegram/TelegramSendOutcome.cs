namespace Ipm.Application.Abstractions.Telegram;

/// <summary>How Telegram answered a send (spec 11 R9).</summary>
public enum TelegramSendOutcome
{
    Ok = 0,

    /// <summary>429: try again after the time Telegram named.</summary>
    RetryAfter = 1,

    /// <summary>403, or chat not found: the user stopped or blocked the bot.</summary>
    Blocked = 2,

    /// <summary>5xx, network or timeout: try again with the back-off.</summary>
    Transient = 3,

    /// <summary>Any other refusal: trying again will not help.</summary>
    Permanent = 4,
}
