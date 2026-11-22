using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Identity.Common;
using Ipm.Application.Features.Identity.Users.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Identity.Users.Commands.SetUserRoles;

internal sealed class SetUserRolesCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, IAccessService access)
    : ICommandHandler<SetUserRolesCommand, UserResponse>
{
    public async ValueTask<Result<UserResponse>> Handle(SetUserRolesCommand command, CancellationToken cancellationToken)
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
            return UserErrors.CannotChangeOwnRoles; // R10, before anything that would hide it
        }

        var outranks = await AccessChecks.EnsureOutranksAsync(caller.Value, user.Id, access, cancellationToken);
        if (outranks.IsFailure)
        {
            return outranks.Error; // R11
        }

        var assignable = await UserChecks.EnsureAssignableAsync(db, caller.Value, command.SystemRoleIds, cancellationToken);
        if (assignable.IsFailure)
        {
            return assignable.Error; // R12, R19
        }

        var remains = await AccessChecks.EnsureRoleManagerRemainsAsync(
            db, snapshot => snapshot.WithUserRoles(user.Id, command.SystemRoleIds), cancellationToken);
        if (remains.IsFailure)
        {
            return remains.Error; // R13
        }

        var changed = user.ChangeRoles(caller.Value.UserId, command.SystemRoleIds);
        if (changed.IsFailure)
        {
            return changed.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await access.InvalidateAsync(user.Id, cancellationToken); // takes effect on the next request
        return user.ToResponse();
    }
}
