using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Identity.Common;
using Ipm.Application.Features.Identity.Users.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Identity.Users.Commands.DeactivateUser;

internal sealed class DeactivateUserCommandHandler(
    IApplicationDbContext db,
    IUnitOfWork unitOfWork,
    IUserContext currentUser,
    ITokenService tokens,
    IAccessService access,
    ISecurityEventLog securityLog) : ICommandHandler<DeactivateUserCommand, UserResponse>
{
    public async ValueTask<Result<UserResponse>> Handle(DeactivateUserCommand command, CancellationToken cancellationToken)
    {
        var caller = await AccessChecks.CallerAsync(currentUser, access, cancellationToken);
        if (caller.IsFailure)
        {
            return caller.Error;
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == command.Id, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        if (!user.HasVersion(command.ExpectedVersion))
        {
            return Error.StaleVersion;
        }

        if (user.Id == caller.Value.UserId)
        {
            return UserErrors.CannotDeactivateSelf; // R9
        }

        var outranks = await AccessChecks.EnsureOutranksAsync(caller.Value, user.Id, access, cancellationToken);
        if (outranks.IsFailure)
        {
            return outranks.Error; // R11
        }

        var remains = await AccessChecks.EnsureRoleManagerRemainsAsync(
            db, snapshot => snapshot.WithUserDeactivated(user.Id), cancellationToken);
        if (remains.IsFailure)
        {
            return remains.Error; // R13
        }

        var deactivated = user.Deactivate(caller.Value.UserId);
        if (deactivated.IsFailure)
        {
            return deactivated.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await tokens.RevokeAllAsync(user.Id, cancellationToken); // R3
        await access.InvalidateAsync(user.Id, cancellationToken);
        await securityLog.LogAsync(SecurityEventType.SessionsRevoked, user.Id, user.UserName, "deactivated", cancellationToken: cancellationToken);
        return user.ToResponse();
    }
}
