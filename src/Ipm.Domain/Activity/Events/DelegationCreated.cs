using Ipm.Domain.Common;

namespace Ipm.Domain.Activity.Events;

/// <summary>Part 08 R11: approval authority was delegated; the delegate is told.</summary>
public sealed record DelegationCreated(Guid DelegationId, Guid FromUserId, Guid ToUserId, DateOnly From, DateOnly To) : DomainEvent;
