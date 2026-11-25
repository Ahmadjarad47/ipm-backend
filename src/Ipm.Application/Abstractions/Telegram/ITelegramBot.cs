using Ipm.Domain.Common;

namespace Ipm.Application.Abstractions.Telegram;

/// <summary>The few Bot API calls the system makes. Tests replace it with a fake; the real one never logs a URL (the token is in it).</summary>
public interface ITelegramBot
{
    /// <summary>R7 and R9: sends HTML text, with an inline button to <paramref name="buttonUrl"/> when given. Never throws for a Telegram or network failure.</summary>
    Task<TelegramSendResult> SendMessageAsync(long chatId, string html, string? buttonUrl, CancellationToken cancellationToken);

    /// <summary>R10: long poll (<paramref name="timeoutSeconds"/> on Telegram side) for the updates from <paramref name="offset"/> on.</summary>
    Task<Result<IReadOnlyList<TelegramUpdate>>> GetUpdatesAsync(long offset, int timeoutSeconds, CancellationToken cancellationToken);

    Task<Result> SetWebhookAsync(string url, string secretToken, CancellationToken cancellationToken);

    Task<Result> DeleteWebhookAsync(CancellationToken cancellationToken);
}
