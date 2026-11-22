namespace Ipm.Application.Abstractions.Messaging;

/// <summary>
/// A query whose result is cached in Redis by <c>QueryCachingBehavior</c>
/// (e.g. dashboard and cost roll-up read models).
/// </summary>
public interface ICachedQuery
{
    string CacheKey { get; }

    /// <summary>Null uses the default expiration of the cache service.</summary>
    TimeSpan? Expiration { get; }
}

public interface ICachedQuery<TResponse> : IQuery<TResponse>, ICachedQuery;
