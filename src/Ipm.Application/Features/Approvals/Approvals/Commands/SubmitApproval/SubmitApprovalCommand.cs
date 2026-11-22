using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Approvals.Approvals.Common;
using Ipm.Domain.Approvals;

namespace Ipm.Application.Features.Approvals.Approvals.Commands.SubmitApproval;

/// <summary>
/// Asks for the approval of an action on a project or a work item with a template (R4). The action (close, reopen, activate) is inferred
/// when omitted. <paramref name="TargetStatusId"/> binds the approval to one target status.
/// </summary>
public sealed record SubmitApprovalCommand(Guid TemplateId, ApprovalSubjectType SubjectType, Guid SubjectId, ApprovalAction? Action, Guid? TargetStatusId)
    : ICommand<ApprovalResponse>;
