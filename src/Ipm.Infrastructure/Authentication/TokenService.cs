using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Ipm.Application.Abstractions.Authentication;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Ipm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Ipm.Infrastructure.Authentication;

/// <summary>
/// JWT access tokens (HMAC-SHA256) carrying only identity, never permissions: <c>sub</c>, <c>email</c>,
/// <c>name</c> and <c>sst</c> (security stamp, so a password change or reset kills old tokens).
/// Refresh tokens are 64 random bytes, stored as SHA-256 hashes, rotated on every use.
/// </summary>
internal sealed class TokenService(ApplicationDbContext db, IOptions<JwtOptions> options, TimeProvider time) : ITokenService
{
    public const string SecurityStampClaim = "sst";

    private readonly JwtOptions _options = options.Value;

    public async Task<AuthTokens> IssueAsync(User user, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var accessExpires = now.AddMinutes(_options.AccessTokenMinutes);

        var accessToken = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = accessExpires.UtcDateTime,
            SigningCredentials = new SigningCredentials(SigningKey(_options), SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Name, user.Name),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                new Claim(SecurityStampClaim, user.SecurityStamp ?? string.Empty),
            ]),
        });

        var (refreshToken, entity) = NewRefreshToken(user.Id, now);
        db.RefreshTokens.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        return new AuthTokens(accessToken, accessExpires, refreshToken, entity.ExpiresAt);
    }

    public async Task<Result<Guid>> RedeemAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var stored = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == Hash(refreshToken), cancellationToken);
        if (stored is null)
        {
            return Result.Failure<Guid>(AuthErrors.RefreshInvalid);
        }

        // Reuse of a rotated token: someone else has it. End every session of this user (R4).
        if (stored.RotatedAt is not null)
        {
            await RevokeAllAsync(stored.UserId, cancellationToken);
            return Result.Failure<Guid>(AuthErrors.RefreshInvalid);
        }

        if (!stored.IsActive(now))
        {
            return Result.Failure<Guid>(AuthErrors.RefreshInvalid);
        }

        // The caller issues the replacement (IssueAsync) right after.
        stored.Rotate(now);
        await db.SaveChangesAsync(cancellationToken);
        return stored.UserId;
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var stored = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == Hash(refreshToken), cancellationToken);
        if (stored is not null)
        {
            stored.Revoke(time.GetUtcNow());
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);
    }

    internal static SymmetricSecurityKey SigningKey(JwtOptions options) => new(Encoding.UTF8.GetBytes(options.SigningKey));

    private (string Token, RefreshToken Entity) NewRefreshToken(Guid userId, DateTimeOffset now)
    {
        var token = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));
        return (token, new RefreshToken
        {
            UserId = userId,
            TokenHash = Hash(token),
            CreatedAt = now,
            ExpiresAt = now.AddDays(_options.RefreshTokenDays),
        });
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
