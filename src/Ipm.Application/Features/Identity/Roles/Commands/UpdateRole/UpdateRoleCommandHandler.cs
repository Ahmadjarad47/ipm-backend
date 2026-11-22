using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Identity.Common;
using Ipm.Application.Features.Identity.Roles.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Identity.Roles.Commands.UpdateRole;

internal sealed class UpdateRoleCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, IAccessService access)
    : ICommandHandler<UpdateRoleCommand, RoleResponse>
{
    public async ValueTask<Result<RoleResponse>> Handle(UpdateRoleCommand command, CancellationToken cancellationToken)
    {
        var caller = await AccessChecks.CallerAsync(currentUser, access, cancellationToken);
        if (caller.IsFailure)
        {
            return caller.Error;
        }

        var role = await db.Roles.SingleOrDefaultAsync(r => r.Id == command.Id, cancellationToken);
        if (role is null)
        {
            return RoleErrors.NotFound;
        }

        if (!role.HasVersion(command.ExpectedVersion))
        {
            return Error.StaleVersion;
        }

        if (command.Scope is { } scope && scope != role.Scope)
        {
            return RoleErrors.ImmutableField; // R15
        }

        var name = command.Name.Trim();
        if (await RoleChecks.NameTakenAsync(db, name, role.Id, cancellationToken))
        {
            return RoleErrors.NameTaken; // R17
        }

        var before = (Granted: role.Granted, Denied: role.Denied);
        role.Update(name, command.Description);
        var changed = role.SetPermissions(command.Permissions, command.Denied); // R18 first: unknown keys are a 400, not a 403
        if (changed.IsFailure)
        {
            return changed.Error;
        }

        // R12: only permissions you hold yourself can be added (removing and denying is always allowed).
        var grantable = AccessChecks.EnsureGrantable(caller.Value, role.Granted.Where(p => !before.Granted.Contains(p)));
        if (grantable.IsFailure)
        {
            return grantable.Error;
        }

        // R13: the edit must leave at least one active user who can manage roles.
        var remains = await AccessChecks.EnsureRoleManagerRemainsAsync(
            db, snapshot => snapshot.WithRolePermissions(role.Id, role.Granted, role.Denied), cancellationToken);
        if (remains.IsFailure)
        {
            return remains.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (!before.Granted.SequenceEqual(role.Granted) || !before.Denied.SequenceEqual(role.Denied))
        {
            await access.InvalidateRoleAsync(role.Id, cancellationToken); // takes effect on the next request
        }

        return role.ToResponse();
    }
}
