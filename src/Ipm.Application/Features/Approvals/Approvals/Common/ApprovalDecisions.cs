using Ipm.Application.Abstractions.Authentication;
using Ipm.Domain.Approvals;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;

namespace Ipm.Application.Features.Approvals.Approvals.Common;

/// <summary>R6: one decision, shared by the single and the bulk use case so every rule applies the same way.</summary>
internal static class ApprovalDecisions
{
    public static async Task<Result> DecideAsync(
        GuardedApproval guarded, DecisionKind decision, string? comment, int revision, ISecurityEventLog securityLog, TimeProvider time, CancellationToken cancellationToken)
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

        if (request.RequesterId == caller.UserId && !ApprovalDeciders.SelfDecisionAllowed(template))
        {
            return ApprovalErrors.SelfDecision;
        }

        if (!guarded.Deciders.IsApprover(request, caller.UserId))
        {
            return ApprovalErrors.NotApprover;
        }

        return request.Decide(caller.UserId, decision, comment, guarded.Deciders.OnBehalfOf(request, caller.UserId), revision, time.GetUtcNow());
    }
}
