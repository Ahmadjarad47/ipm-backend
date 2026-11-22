namespace Ipm.Infrastructure.Authentication;

/// <summary>
/// A refresh token of one sign-in session. Only its SHA-256 hash is stored. Each use replaces it with a
/// new one (rotation); presenting a token that was already replaced means it leaked, so every session of
/// that user is revoked (R4).
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public required Guid UserId { get; init; }

    public required string TokenHash { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>Set when this token was exchanged for a new one. Presenting it again means it leaked.</summary>
    public DateTimeOffset? RotatedAt { get; private set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    public void Revoke(DateTimeOffset at) => RevokedAt ??= at;

    public void Rotate(DateTimeOffset at)
    {
        RotatedAt ??= at;
        Revoke(at);
    }
}
