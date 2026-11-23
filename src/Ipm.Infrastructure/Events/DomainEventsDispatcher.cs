using System.Collections.Concurrent;
using System.Reflection;
using Ipm.Application.Abstractions.Events;
using Ipm.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Ipm.Infrastructure.Events;

/// <summary>Resolves every <see cref="IDomainEventHandler{TEvent}"/> for each event and runs them in order.</summary>
public sealed class DomainEventsDispatcher(IServiceProvider serviceProvider, Microsoft.Extensions.Logging.ILogger<DomainEventsDispatcher> logger)
{
    private static readonly ConcurrentDictionary<Type, (Type HandlerType, MethodInfo Handle)> Cache = new ConcurrentDictionary<Type, (Type HandlerType, MethodInfo Handle)>();

    public async Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken)
    {
        foreach (IDomainEvent domainEvent in domainEvents)
        {
            var (handlerType, handle) = Cache.GetOrAdd(key: domainEvent.GetType(), static eventType =>
            {
                var type = typeof(IDomainEventHandler<>).MakeGenericType(eventType);
                return (type, type.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.Handle))!);
            });

            foreach (object? handler in serviceProvider.GetServices(handlerType))
            {
                // The change is already committed: a failing reaction (a notification, a cache) is logged and never fails or undoes the request.
                try
                {
                    await (Task)handle.Invoke(handler, [domainEvent, cancellationToken])!;
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    Microsoft.Extensions.Logging.LoggerExtensions.LogError(logger, e, "Handler {Handler} failed for {Event}", handler!.GetType().Name, domainEvent.GetType().Name);
                }
            }
        }
    }
}
