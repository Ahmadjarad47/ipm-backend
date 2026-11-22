using Ipm.Domain.Common;

namespace Ipm.Application.Abstractions.Events;

/// <summary>
/// Reacts to a domain event after the transaction that raised it has been committed
/// (e.g. invalidate cached read models, push a SignalR notification).
/// </summary>
public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task Handle(TEvent domainEvent, CancellationToken cancellationToken);
}
