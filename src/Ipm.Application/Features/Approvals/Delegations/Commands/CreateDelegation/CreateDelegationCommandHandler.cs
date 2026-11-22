using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common;
using Ipm.Application.Features.Approvals.Delegations.Common;
using Ipm.Application.Features.Identity.Common;
using Ipm.Domain.Approvals;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Approvals.Delegations.Commands.CreateDelegation;

internal sealed class CreateDelegationCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog, TimeProvider time)
    : ICommandHandler<CreateDelegationCommand, DelegationResponse>
{
    public async ValueTask<Result<DelegationResponse>> Handle(CreateDelegationCommand command, CancellationToken cancellationToken)
    {
        var caller = await AccessChecks.CallerAsync(currentUser, access, cancellationToken);
        if (caller.IsFailure)
        {
            return caller.Error;
        }

        var fromId = command.FromUserId ?? caller.Value.UserId;
        if (fromId != caller.Value.UserId && !caller.Value.Has(Permissions.ApprovalsConfigure))
        {
            await securityLog.LogAsync(SecurityEventType.PermissionDenied, caller.Value.UserId, null, permission: Permissions.ApprovalsConfigure, cancellationToken: cancellationToken);
            return AuthErrors.Forbidden;
        }

        var from = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == fromId, cancellationToken);
        if (from is not { IsActive: true })
        {
            return ApprovalErrors.DelegatorInvalid;
        }

        var to = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == command.ToUserId, cancellationToken);
        if (to is not { IsActive: true })
        {
            return Error.Validation("Delegation.ToUserInvalid", "المفوَّض إليه غير موجود أو غير نشط.");
        }

        var delegation = Delegation.Create(fromId, to.Id, command.From, command.To, command.Reason, RiyadhClock.Today(time));
        if (delegation.IsFailure)
        {
            return delegation.Error;
        }

        // The delegate must be able to decide somewhere, or the hand-over means nothing.
        var delegateAccess = await access.GetAsync(to.Id, cancellationToken);
        if (delegateAccess is null || !(delegateAccess.Has(Permissions.ApprovalsDecide) || delegateAccess.ProjectPermissions.Any(p => p.Value.Contains(Permissions.ApprovalsDecide))))
        {
            return Error.Validation("Delegation.ToUserCannotDecide", "المفوَّض إليه لا يملك صلاحية القرار في أي مشروع.");
        }

        var overlapping = await db.Delegations.AnyAsync(
            d => d.IsActive && d.FromUserId == fromId && d.ToUserId == to.Id && d.FromDate <= command.To && command.From <= d.ToDate, cancellationToken);
        if (overlapping)
        {
            return ApprovalErrors.DelegationOverlap;
        }

        db.Delegations.Add(delegation.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return delegation.Value.ToResponse();
    }
}
