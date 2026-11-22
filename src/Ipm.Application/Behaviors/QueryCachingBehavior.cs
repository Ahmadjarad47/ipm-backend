using System.Reflection;
using Ipm.Application.Abstractions.Caching;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Domain.Common;
using Mediator;

namespace Ipm.Application.Behaviors;

/// <summary>
/// Serves <see cref="ICachedQuery"/> results from Redis, and stores successful results on a miss.
/// Only the value of a successful <c>Result&lt;T&gt;</c> is cached; failures are never cached.
/// Invalidation happens in domain event handlers.
/// </summary>
public sealed class QueryCachingBehavior<TMessage, TResponse>(ICacheService cache)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : ICachedQuery, IMessage
    where TResponse : Result
{
    // Resolved once per closed generic type: Result<TValue> -> TValue, its Value getter and Result.Success<TValue>.
    private static readonly Type ValueType = typeof(TResponse).GenericTypeArguments.Single();

    private static readonly PropertyInfo ValueProperty = typeof(TResponse).GetProperty(nameof(Result<object>.Value))!;

    private static readonly MethodInfo SuccessFactory = typeof(Result)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(m => m.Name == nameof(Result.Success) && m.IsGenericMethodDefinition)
        .MakeGenericMethod(ValueType);

    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        var cached = await cache.GetAsync(message.CacheKey, ValueType, cancellationToken);
        if (cached is not null)
        {
            return (TResponse)SuccessFactory.Invoke(null, [cached])!;
        }

        var response = await next(message, cancellationToken);
        if (response.IsSuccess)
        {
            await cache.SetAsync(message.CacheKey, ValueProperty.GetValue(response)!, ValueType, message.Expiration, cancellationToken);
        }

        return response;
    }
}
