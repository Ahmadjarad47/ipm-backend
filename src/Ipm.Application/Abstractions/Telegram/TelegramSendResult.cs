namespace Ipm.Application.Abstractions.Telegram;

/// <summary>The classified answer to a send. <see cref="Error"/> is a short description that never contains the bot token.</summary>
public sealed record TelegramSendResult(TelegramSendOutcome Outcome, TimeSpan? RetryAfter = null, string? Error = null)
{
    public static readonly TelegramSendResult Ok = new(TelegramSendOutcome.Ok);

    public static TelegramSendResult After(TimeSpan retryAfter) => new(TelegramSendOutcome.RetryAfter, retryAfter, "429 Too Many Requests");

    public static TelegramSendResult Blocked(string error) => new(TelegramSendOutcome.Blocked, null, error);

    public static TelegramSendResult Transient(string error) => new(TelegramSendOutcome.Transient, null, error);

    public static TelegramSendResult Permanent(string error) => new(TelegramSendOutcome.Permanent, null, error);
}
