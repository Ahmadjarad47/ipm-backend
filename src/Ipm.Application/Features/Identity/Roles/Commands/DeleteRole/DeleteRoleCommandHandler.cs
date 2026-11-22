using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Identity.Roles.Commands.DeleteRole;

internal sealed class DeleteRoleCommandHandler(IApplicationDbContext db, IUnitOfWork unitOfWork) : ICommandHandler<DeleteRoleCommand>
{
    public async ValueTask<Result> Handle(DeleteRoleCommand command, CancellationToken cancellationToken)
    {
        var role = await db.Roles.SingleOrDefaultAsync(r => r.Id == command.Id, cancellationToken);
        if (role is null)
        {
            return RoleErrors.NotFound;
        }

        if (!role.HasVersion(command.ExpectedVersion))
        {
            return Error.StaleVersion;
        }

        var deletable = role.EnsureDeletable(); // R15
        if (deletable.IsFailure)
        {
            return deletable;
        }

        // R16: a system role is held through the user, a project role through project membership.
        var holders = await db.Users.CountAsync(u => u.RoleIds.Contains(command.Id), cancellationToken)
            + await db.ProjectMembers.CountAsync(m => m.RoleId == command.Id, cancellationToken);
        if (holders > 0)
        {
            return RoleErrors.InUse(holders); // R16
        }

        db.Roles.Remove(role);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
