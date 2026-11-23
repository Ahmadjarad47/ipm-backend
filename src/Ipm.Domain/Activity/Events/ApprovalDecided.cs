using Ipm.Domain.Common;

namespace Ipm.Domain.Activity.Events;

/// <summary>Part 08 R11: a stage was decided; the requester is told.</summary>
public sealed record ApprovalDecided(Guid RequestId, Guid DeciderId, string Decision) : DomainEvent;
