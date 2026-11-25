using System.Security.Cryptography;
using System.Text;
using Ipm.Application.Abstractions.Caching;
using Ipm.Application.Abstractions.Telegram;

namespace Ipm.Infrastructure.Telegram;

/// <summary>
/// Spec 11 R2 and R3 over Redis. A token is 32 random bytes in base64url (43 characters, within the 64 Telegram allows for <c>start</c>). Only its
/// SHA-256 is a key, so a look at Redis does not give a usable token. A second key per user remembers the current one, so a new token cancels the old.
/// </summary>
internal sealed class RedisTelegramLinkTokens(ICacheService cache, TimeProvider time) : ITelegramLinkTokens
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    public async Task<TelegramLinkToken> IssueAsync(Guid userId, CancellationToken cancellationToken)
    {
        var token = Base64Url(RandomNumberGenerator.GetBytes(32));
        var key = TokenKey(Hash(token));
        var userKey = UserKey(userId);

        if (await cache.GetAsync<string>(userKey, cancellationToken) is { } previous)
        {
            await cache.RemoveAsync(previous, cancellationToken);
        }

        await cache.SetAsync(key, userId.ToString(), Lifetime, cancellationToken);
        await cache.SetAsync(userKey, key, Lifetime, cancellationToken);
        return new TelegramLinkToken(token, time.GetUtcNow() + Lifetime);
    }

    public async Task<Guid?> ConsumeAsync(string token, CancellationToken cancellationToken)
    {
        // Anything that cannot be one of ours is not even looked up.
        if (token.Length is 0 or > 64 || !token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
        {
            return null;
        }

        var key = TokenKey(Hash(token));
        var owner = await cache.GetAsync<string>(key, cancellationToken);
        if (owner is null || !Guid.TryParse(owner, out var userId))
        {
            return null;
        }

        await cache.RemoveAsync(key, cancellationToken);
        await cache.RemoveAsync(UserKey(userId), cancellationToken);
        return userId;
    }

    private static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string TokenKey(string hash) => $"telegram:link:{hash}";

    private static string UserKey(Guid userId) => $"telegram:link-user:{userId:N}";

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
