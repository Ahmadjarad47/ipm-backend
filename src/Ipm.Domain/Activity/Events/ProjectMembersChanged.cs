using Ipm.Domain.Common;

namespace Ipm.Domain.Activity.Events;

/// <summary>Part 08 R11: members were added to or removed from a project.</summary>
public sealed record ProjectMembersChanged(Guid ProjectId, string Name, IReadOnlyList<Guid> Added, IReadOnlyList<Guid> Removed) : DomainEvent;
