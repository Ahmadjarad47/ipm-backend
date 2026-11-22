using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Identity.Common;
using Ipm.Application.Features.Identity.Roles.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Identity.Roles.Commands.CreateRole;

internal sealed class CreateRoleCommandHandler(IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, IAccessService access)
    : ICommandHandler<CreateRoleCommand, RoleResponse>
{
    public async ValueTask<Result<RoleResponse>> Handle(CreateRoleCommand command, CancellationToken cancellationToken)
    {
        var caller = await AccessChecks.CallerAsync(currentUser, access, cancellationToken);
        if (caller.IsFailure)
        {
            return caller.Error;
        }

        var name = command.Name.Trim();
        if (await RoleChecks.NameTakenAsync(db, name, null, cancellationToken))
        {
            return RoleErrors.NameTaken; // R17
        }

        IReadOnlyList<string> granted = command.Permissions;
        IReadOnlyList<string> denied = command.Denied;
        if (command.CopyFromRoleId is { } sourceId && granted.Count == 0 && denied.Count == 0)
        {
            var source = await db.Roles.AsNoTracking().SingleOrDefaultAsync(r => r.Id == sourceId, cancellationToken);
            if (source is null)
            {
                return RoleErrors.NotFound;
            }

            granted = source.Granted;
            denied = source.Denied;
        }

        var created = Role.Create(name, command.Description, command.Scope, granted, denied); // R18 first: unknown keys are a 400, not a 403
        if (created.IsFailure)
        {
            return created.Error;
        }

        var grantable = AccessChecks.EnsureGrantable(caller.Value, created.Value.Granted);
        if (grantable.IsFailure)
        {
            return grantable.Error; // R12
        }

        db.Roles.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return created.Value.ToResponse();
    }
}
