namespace Ipm.Application.Abstractions.Telegram;

/// <summary>The one-time tokens of the Start deep link (spec 11 R2 and R3). They live in the cache, never in the database.</summary>
public interface ITelegramLinkTokens
{
    /// <summary>Makes a token for the user and invalidates the one they had before.</summary>
    Task<TelegramLinkToken> IssueAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>The user the token belongs to, and the token is gone. Null for an unknown, used, replaced or expired token.</summary>
    Task<Guid?> ConsumeAsync(string token, CancellationToken cancellationToken);
}
