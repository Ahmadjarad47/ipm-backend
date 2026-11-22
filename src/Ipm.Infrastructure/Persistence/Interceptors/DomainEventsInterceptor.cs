using Ipm.Domain.Common;
using Ipm.Infrastructure.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Ipm.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Collects domain events raised by tracked entities and dispatches them once the
/// transaction has been committed, so handlers never see uncommitted data.
/// </summary>
public sealed class DomainEventsInterceptor(DomainEventsDispatcher dispatcher) : SaveChangesInterceptor
{
    private List<IDomainEvent> _pending = [];

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            _pending = Collect(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        var events = _pending;
        _pending = [];
        await dispatcher.DispatchAsync(events, cancellationToken);

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        _pending = [];
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private static List<IDomainEvent> Collect(DbContext context)
    {
        var entities = context.ChangeTracker.Entries<Entity>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Count > 0)
            .ToList();

        var events = entities.SelectMany(e => e.DomainEvents).ToList();
        entities.ForEach(e => e.ClearDomainEvents());

        return events;
    }
}
