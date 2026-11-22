namespace Ipm.Application.Abstractions.Caching;

/// <summary>
/// Distributed cache (Redis). Never the source of truth: everything stored here
/// can be rebuilt from PostgreSQL.
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    Task<object?> GetAsync(string key, Type type, CancellationToken cancellationToken = default);

    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);

    Task SetAsync(string key, object value, Type type, TimeSpan? expiration = null, CancellationToken cancellationToken = default);

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}
