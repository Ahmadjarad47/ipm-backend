using Ipm.Domain.Common;

namespace Ipm.Domain.Activity.Events;

/// <summary>Part 08 R11: someone reminded the approvers of the current stage.</summary>
public sealed record ApprovalReminded(Guid RequestId) : DomainEvent;
