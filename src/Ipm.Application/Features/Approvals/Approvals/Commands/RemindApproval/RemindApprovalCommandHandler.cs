using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Approvals.Approvals.Common;
using Ipm.Domain.Approvals;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;

namespace Ipm.Application.Features.Approvals.Approvals.Commands.RemindApproval;

internal sealed class RemindApprovalCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, IAccessService access, TimeProvider time)
    : ICommandHandler<RemindApprovalCommand, ApprovalResponse>
{
    public async ValueTask<Result<ApprovalResponse>> Handle(RemindApprovalCommand command, CancellationToken cancellationToken)
    {
        var guarded = await ApprovalGuard.LoadAsync(db, currentUser, access, time, command.Id, track: true, cancellationToken);
        if (guarded.IsFailure)
        {
            return guarded.Error;
        }

        var (request, caller) = (guarded.Value.Request, guarded.Value.Caller);
        if (request.RequesterId != caller.UserId && !caller.Can(Permissions.ApprovalsConfigure, request.ProjectId))
        {
            return ApprovalErrors.RemindForbidden;
        }

        var reminded = request.Remind(time.GetUtcNow()); // R14: the notification itself is part 08
        if (reminded.IsFailure)
        {
            return reminded.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return request.ToResponse(time.GetUtcNow(), guarded.Value.CanDecide);
    }
}
