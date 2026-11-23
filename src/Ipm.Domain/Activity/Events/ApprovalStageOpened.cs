using Ipm.Domain.Common;

namespace Ipm.Domain.Activity.Events;

/// <summary>Part 08 R11: an approval request was submitted or reached a new stage; the stage approvers (and their delegates) are told.</summary>
public sealed record ApprovalStageOpened(Guid RequestId) : DomainEvent;
