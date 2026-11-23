using Ipm.Domain.Common;

namespace Ipm.Domain.Activity.Events;

/// <summary>Part 08 R11: a project moved to another status.</summary>
public sealed record ProjectStatusChanged(Guid ProjectId, string Name, Guid FromStatusId, Guid ToStatusId) : DomainEvent;
