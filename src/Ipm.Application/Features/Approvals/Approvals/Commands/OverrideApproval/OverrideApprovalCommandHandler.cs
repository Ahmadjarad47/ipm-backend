using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Approvals.Approvals.Common;
using Ipm.Domain.Approvals;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;

namespace Ipm.Application.Features.Approvals.Approvals.Commands.OverrideApproval;

internal sealed class OverrideApprovalCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog, TimeProvider time)
    : ICommandHandler<OverrideApprovalCommand, ApprovalResponse>
{
    public async ValueTask<Result<ApprovalResponse>> Handle(OverrideApprovalCommand command, CancellationToken cancellationToken)
    {
        var guarded = await ApprovalGuard.LoadAsync(db, currentUser, access, time, command.Id, track: true, cancellationToken);
        if (guarded.IsFailure)
        {
            return guarded.Error;
        }

        var (request, template, caller) = (guarded.Value.Request, guarded.Value.Template, guarded.Value.Caller);
        if (!caller.Has(Permissions.AuditOverride))
        {
            await securityLog.LogAsync(SecurityEventType.PermissionDenied, caller.UserId, null, permission: Permissions.AuditOverride, cancellationToken: cancellationToken);
            return AuthErrors.Forbidden;
        }

        if (!request.HasVersion(command.ExpectedVersion))
        {
            return Error.StaleVersion;
        }

        var decidable = request.EnsureDecidable(command.Revision);
        if (decidable.IsFailure)
        {
            return decidable.Error;
        }

        // Same rule as the last approval (06b R6): a sensitive change is applied in the same transaction, and a failure fails the decision.
        if (command.Decision == DecisionKind.Approve && template?.Event == ApprovalEvent.SensitiveChange)
        {
            var applied = await SensitiveChanges.ApplyAsync(db, access, request, time.GetUtcNow(), cancellationToken);
            if (applied.IsFailure)
            {
                return applied.Error;
            }
        }

        var overridden = request.Override(caller.UserId, command.Decision, command.Comment, command.Revision, time.GetUtcNow());
        if (overridden.IsFailure)
        {
            return overridden.Error;
        }

        await ProjectApprovalOutcome.ApplyAsync(db, request, template, caller.UserId, command.Comment, overridden: true, time, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return request.ToResponse(time.GetUtcNow(), false);
    }
}
