using Ipm.Domain.Common;

namespace Ipm.Domain.Activity.Events;

/// <summary>Part 08 R11: a pending approval was voided by a change of a protected field; the requester is told.</summary>
public sealed record ApprovalSuperseded(Guid RequestId) : DomainEvent;
