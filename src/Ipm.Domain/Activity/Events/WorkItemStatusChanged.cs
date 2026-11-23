using Ipm.Domain.Common;

namespace Ipm.Domain.Activity.Events;

/// <summary>Part 08 R11: a work item moved to another status.</summary>
public sealed record WorkItemStatusChanged(Guid WorkItemId, Guid ProjectId, string Ref, string Title, Guid FromStatusId, Guid ToStatusId) : DomainEvent;
