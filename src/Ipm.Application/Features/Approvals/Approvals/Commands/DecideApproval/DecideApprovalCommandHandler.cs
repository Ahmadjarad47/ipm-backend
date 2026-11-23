using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Approvals.Approvals.Common;
using Ipm.Domain.Common;

namespace Ipm.Application.Features.Approvals.Approvals.Commands.DecideApproval;

internal sealed class DecideApprovalCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog, TimeProvider time)
    : ICommandHandler<DecideApprovalCommand, ApprovalResponse>
{
    public async ValueTask<Result<ApprovalResponse>> Handle(DecideApprovalCommand command, CancellationToken cancellationToken)
    {
        var guarded = await ApprovalGuard.LoadAsync(db, currentUser, access, time, command.Id, track: true, cancellationToken);
        if (guarded.IsFailure)
        {
            return guarded.Error;
        }

        if (!guarded.Value.Request.HasVersion(command.ExpectedVersion))
        {
            return Error.StaleVersion;
        }

        var decided = await ApprovalDecisions.DecideAsync(guarded.Value, command.Decision, command.Comment, command.Revision, db, access, securityLog, time, cancellationToken);
        if (decided.IsFailure)
        {
            return decided.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        var request = guarded.Value.Request;
        return request.ToResponse(time.GetUtcNow(), guarded.Value.Deciders.CanDecide(request, guarded.Value.Caller.UserId, guarded.Value.Template));
    }
}
