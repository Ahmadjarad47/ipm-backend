using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Identity.Common;
using Ipm.Domain.Approvals;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Approvals.Delegations.Commands.RevokeDelegation;

internal sealed class RevokeDelegationCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog)
    : ICommandHandler<RevokeDelegationCommand>
{
    public async ValueTask<Result> Handle(RevokeDelegationCommand command, CancellationToken cancellationToken)
    {
        var caller = await AccessChecks.CallerAsync(currentUser, access, cancellationToken);
        if (caller.IsFailure)
        {
            return caller.Error;
        }

        var delegation = await db.Delegations.SingleOrDefaultAsync(d => d.Id == command.Id, cancellationToken);
        var manager = caller.Value.Has(Permissions.ApprovalsConfigure);
        if (delegation is null || (delegation.FromUserId != caller.Value.UserId && delegation.ToUserId != caller.Value.UserId && !manager))
        {
            return ApprovalErrors.DelegationNotFound;
        }

        if (delegation.FromUserId != caller.Value.UserId && !manager)
        {
            await securityLog.LogAsync(SecurityEventType.PermissionDenied, caller.Value.UserId, null, permission: Permissions.ApprovalsConfigure, cancellationToken: cancellationToken);
            return AuthErrors.Forbidden;
        }

        var revoked = delegation.Revoke();
        if (revoked.IsFailure)
        {
            return revoked.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
