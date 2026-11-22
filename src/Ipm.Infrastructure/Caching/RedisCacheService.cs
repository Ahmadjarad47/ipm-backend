using System.Text.Json;
using Ipm.Application.Abstractions.Caching;
using Microsoft.Extensions.Caching.Distributed;

namespace Ipm.Infrastructure.Caching;

/// <summary><see cref="ICacheService"/> over Redis (IDistributedCache) with System.Text.Json payloads.</summary>
public sealed class RedisCacheService(IDistributedCache cache) : ICacheService
{
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(5);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
        (T?)await GetAsync(key, typeof(T), cancellationToken);

    public async Task<object?> GetAsync(string key, Type type, CancellationToken cancellationToken = default)
    {
        var bytes = await cache.GetAsync(key, cancellationToken);
        return bytes is null ? null : JsonSerializer.Deserialize(bytes, type, JsonOptions);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) =>
        SetAsync(key, value!, typeof(T), expiration, cancellationToken);

    public Task SetAsync(string key, object value, Type type, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, type, JsonOptions);
        var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiration ?? DefaultExpiration };
        return cache.SetAsync(key, bytes, options, cancellationToken);
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        cache.RemoveAsync(key, cancellationToken);
}
