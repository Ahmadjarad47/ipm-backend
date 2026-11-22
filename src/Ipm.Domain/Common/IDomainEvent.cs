namespace Ipm.Domain.Common;

/// <summary>
/// Something that happened in the domain (e.g. a purchase stage was completed).
/// Raised by entities and dispatched after the transaction commits.
/// </summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}

public abstract record DomainEvent : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
