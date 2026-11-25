using Ipm.Domain.Common;

namespace Ipm.Domain.Activity;

/// <summary>
/// The Telegram chat of one user (spec 11 R3 and R4). Audited: linking an account is a security change.
/// A blocked bot disables the link (R9) instead of deleting it, so the card can say so and a new Start re-enables it.
/// </summary>
public sealed class TelegramLink : AuditableEntity
{
    public const int UsernameMaxLength = 64;

    private TelegramLink() { }

    private TelegramLink(Guid id) : base(id) { }

    public Guid UserId { get; private set; }

    public long ChatId { get; private set; }

    public string? TelegramUsername { get; private set; }

    public DateTimeOffset LinkedAt { get; private set; }

    public DateTimeOffset? DisabledAt { get; private set; }

    public bool IsActive => DisabledAt is null;

    public static TelegramLink Create(Guid userId, long chatId, string? telegramUsername, DateTimeOffset at) =>
        new(Guid.CreateVersion7()) { UserId = userId, ChatId = chatId, TelegramUsername = Clean(telegramUsername), LinkedAt = at };

    /// <summary>A new Start from a (possibly different) chat: the link points there and is on again.</summary>
    public void Relink(long chatId, string? telegramUsername, DateTimeOffset at)
    {
        ChatId = chatId;
        TelegramUsername = Clean(telegramUsername);
        LinkedAt = at;
        DisabledAt = null;
    }

    /// <summary>R9: Telegram refused the bot (blocked or chat gone). Disabling twice keeps the first time.</summary>
    public void Disable(DateTimeOffset at) => DisabledAt ??= at;

    private static string? Clean(string? username)
    {
        var clean = new string((username ?? string.Empty).Where(c => !char.IsControl(c)).ToArray()).Trim();
        return clean.Length == 0 ? null : clean.Length > UsernameMaxLength ? clean[..UsernameMaxLength] : clean;
    }
}
