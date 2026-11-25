namespace Ipm.Domain.Activity;

/// <summary>
/// One message waiting to go to Telegram (spec 11 R6 to R9): the outbox row written in the same save as the notification, one per notification.
/// A record like <see cref="Notification"/>, not an audited entity. The worker only ever moves it forward: sent, failed or expired.
/// </summary>
public sealed class TelegramMessage
{
    public const int TextMaxLength = 4096;
    public const int ErrorMaxLength = 500;
    public const int UrlMaxLength = 500;
    public const int MaxAttempts = 8;

    public static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    private TelegramMessage() { }

    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid NotificationId { get; private init; }

    public Guid UserId { get; private init; }

    public long ChatId { get; private init; }

    public string Text { get; private init; } = string.Empty;

    /// <summary>The absolute link into the app, for the inline button (only sent when it is https).</summary>
    public string? Url { get; private init; }

    public TelegramMessageStatus Status { get; private set; } = TelegramMessageStatus.Pending;

    public int Attempts { get; private set; }

    public DateTimeOffset NextAttemptAt { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset CreatedAt { get; private init; }

    public DateTimeOffset? SentAt { get; private set; }

    public static TelegramMessage Create(Guid notificationId, Guid userId, long chatId, string text, string? url, DateTimeOffset at) =>
        new()
        {
            NotificationId = notificationId,
            UserId = userId,
            ChatId = chatId,
            Text = text.Length > TextMaxLength ? text[..TextMaxLength] : text,
            Url = url,
            NextAttemptAt = at,
            CreatedAt = at,
        };

    /// <summary>R9: 5 s, 10 s, 20 s… for the first, second, third failed attempt, never more than 10 minutes.</summary>
    public static TimeSpan Backoff(int failedAttempts)
    {
        var exponent = Math.Clamp(failedAttempts - 1, 0, 20);
        var delay = TimeSpan.FromTicks(BaseDelay.Ticks << exponent);
        return delay > MaxDelay ? MaxDelay : delay;
    }

    public bool IsPending => Status == TelegramMessageStatus.Pending;

    /// <summary>R9: waiting more than 24 hours means the news is stale.</summary>
    public bool IsStale(DateTimeOffset now) => IsPending && now - CreatedAt > Lifetime;

    public void MarkSent(DateTimeOffset at)
    {
        Attempts++;
        Status = TelegramMessageStatus.Sent;
        SentAt = at;
        LastError = null;
    }

    /// <summary>A failed attempt that may be repeated: later by the back-off, or by <paramref name="delay"/> when given. The 8th failure is final.</summary>
    public void Retry(DateTimeOffset now, string error, TimeSpan? delay = null)
    {
        Attempts++;
        LastError = Trim(error);
        if (Attempts >= MaxAttempts)
        {
            Status = TelegramMessageStatus.Failed;
            return;
        }

        NextAttemptAt = now + (delay ?? Backoff(Attempts));
    }

    /// <summary>Not an attempt: the rate limit (ours per chat, or Telegram 429) asks to try later.</summary>
    public void Defer(DateTimeOffset now, TimeSpan delay, string? reason = null)
    {
        NextAttemptAt = now + delay;
        if (reason is not null)
        {
            LastError = Trim(reason);
        }
    }

    public void Fail(string error)
    {
        Attempts++;
        Status = TelegramMessageStatus.Failed;
        LastError = Trim(error);
    }

    public void Expire()
    {
        if (IsPending)
        {
            Status = TelegramMessageStatus.Expired;
            LastError = "انتهت صلاحية الرسالة قبل إرسالها.";
        }
    }

    private static string Trim(string text) => text.Length > ErrorMaxLength ? text[..ErrorMaxLength] : text;
}
