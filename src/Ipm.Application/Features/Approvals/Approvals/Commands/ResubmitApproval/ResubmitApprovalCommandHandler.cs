using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Approvals.Approvals.Common;
using Ipm.Domain.Approvals;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;

namespace Ipm.Application.Features.Approvals.Approvals.Commands.ResubmitApproval;

internal sealed class ResubmitApprovalCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog, TimeProvider time)
    : ICommandHandler<ResubmitApprovalCommand, ApprovalResponse>
{
    public async ValueTask<Result<ApprovalResponse>> Handle(ResubmitApprovalCommand command, CancellationToken cancellationToken)
    {
        var guarded = await ApprovalGuard.LoadAsync(db, currentUser, access, time, command.Id, track: true, cancellationToken);
        if (guarded.IsFailure)
        {
            return guarded.Error;
        }

        var (request, project, caller) = (guarded.Value.Request, guarded.Value.Project, guarded.Value.Caller);
        if (request.RequesterId != caller.UserId)
        {
            return ApprovalErrors.NotRequester;
        }

        if (!caller.Can(Permissions.ApprovalsRequest, project.Id))
        {
            await securityLog.LogAsync(SecurityEventType.PermissionDenied, caller.UserId, null, permission: Permissions.ApprovalsRequest, cancellationToken: cancellationToken);
            return AuthErrors.Forbidden;
        }

        if (!request.HasVersion(command.ExpectedVersion))
        {
            return Error.StaleVersion;
        }

        if (request.Status is not (ApprovalStatus.Returned or ApprovalStatus.Superseded))
        {
            return ApprovalErrors.NotResubmittable;
        }

        if (project.IsArchived)
        {
            return ApprovalErrors.ProjectArchived;
        }

        var subject = await ApprovalSubjects.LoadAsync(db, request.SubjectType, request.SubjectId, cancellationToken);
        if (subject is null)
        {
            return ApprovalErrors.SubjectNotFound;
        }

        var (resolved, unresolved) = await ApproverResolver.FreezeAsync(db, request.Stages, project, request.RequesterId, cancellationToken);
        if (unresolved is not null)
        {
            return ApprovalErrors.StageUnresolvable(unresolved.Order, unresolved.Name);
        }

        var snapshot = await ApprovalSubjects.SnapshotAsync(db, subject, request.Snapshot.Select(s => s.Field), cancellationToken);
        var resubmitted = request.Resubmit(snapshot, resolved, time.GetUtcNow());
        if (resubmitted.IsFailure)
        {
            return resubmitted.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return request.ToResponse(time.GetUtcNow(), canDecide: false);
    }
}
