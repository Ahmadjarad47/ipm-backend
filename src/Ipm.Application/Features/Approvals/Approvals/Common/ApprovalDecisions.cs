using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Domain.Approvals;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;

namespace Ipm.Application.Features.Approvals.Approvals.Common;

/// <summary>R6: one decision, shared by the single and the bulk use case so every rule applies the same way.</summary>
internal static class ApprovalDecisions
{
    public static async Task<Result> DecideAsync(
        GuardedApproval guarded, DecisionKind decision, string? comment, int revision, IApplicationDbContext db, IAccessService access, ISecurityEventLog securityLog, TimeProvider time,
        CancellationToken cancellationToken)
    {
        var (request, template, caller) = (guarded.Request, guarded.Template, guarded.Caller);

        if (!caller.Can(Permissions.ApprovalsDecide, request.ProjectId))
        {
            await securityLog.LogAsync(SecurityEventType.PermissionDenied, caller.UserId, null, permission: Permissions.ApprovalsDecide, cancellationToken: cancellationToken);
            return AuthErrors.Forbidden;
        }

        var decidable = request.EnsureDecidable(revision);
        if (decidable.IsFailure)
        {
            return decidable;
        }

        if (request.RequesterId == caller.UserId && !guarded.Deciders.SelfDecisionAllowed(template))
        {
            return ApprovalErrors.SelfDecision;
        }

        if (!guarded.Deciders.IsApprover(request, caller.UserId))
        {
            return ApprovalErrors.NotApprover;
        }

        // 06b R6: the last approval of a sensitive change applies the proposed values in the same transaction; if that cannot be done, the decision fails.
        var final = decision == DecisionKind.Approve && !request.Stages.Exists(s => s.Order == request.CurrentStage + 1);
        if (final && template?.Event == ApprovalEvent.SensitiveChange)
        {
            var applied = await SensitiveChanges.ApplyAsync(db, access, request, time.GetUtcNow(), cancellationToken);
            if (applied.IsFailure)
            {
                return applied;
            }
        }

        var decided = request.Decide(caller.UserId, decision, comment, guarded.Deciders.OnBehalfOf(request, caller.UserId), revision, time.GetUtcNow());
        if (decided.IsSuccess)
        {
            // A project approval decides the project's status by itself: approved → active, rejected → cancelled.
            await ProjectApprovalOutcome.ApplyAsync(db, request, template, caller.UserId, comment, overridden: false, time, cancellationToken);
        }

        return decided;
    }
}
