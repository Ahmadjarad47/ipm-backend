using Ipm.Domain.Common;

namespace Ipm.Domain.Activity.Events;

/// <summary>Part 08 R11: people were assigned to or removed from a work item (also raised when it is created with assignees).</summary>
public sealed record WorkItemAssigneesChanged(Guid WorkItemId, Guid ProjectId, string Ref, string Title, IReadOnlyList<Guid> Added, IReadOnlyList<Guid> Removed) : DomainEvent;
