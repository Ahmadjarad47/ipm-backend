using Ipm.Domain.Common;
using Ipm.Domain.Identity;

namespace Ipm.Application.Abstractions.Authentication;

/// <summary>Issues the JWT access token and manages rotating refresh tokens (stored hashed).</summary>
public interface ITokenService
{
    /// <summary>Starts a new session: a fresh access token and a fresh refresh token.</summary>
    Task<AuthTokens> IssueAsync(User user, CancellationToken cancellationToken);

    /// <summary>
    /// Rotates a refresh token. Fails when it is unknown, expired or revoked; presenting an already
    /// rotated token revokes every session of its user (R4).
    /// </summary>
    Task<Result<Guid>> RedeemAsync(string refreshToken, CancellationToken cancellationToken);

    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken);

    /// <summary>Ends every session of the user (deactivation, password change or reset).</summary>
    Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken);
}
