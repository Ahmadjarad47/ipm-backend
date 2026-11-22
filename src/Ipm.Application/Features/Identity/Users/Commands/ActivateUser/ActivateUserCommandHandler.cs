using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Identity.Common;
using Ipm.Application.Features.Identity.Users.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Identity.Users.Commands.ActivateUser;

internal sealed class ActivateUserCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, IAccessService access)
    : ICommandHandler<ActivateUserCommand, UserResponse>
{
    public async ValueTask<Result<UserResponse>> Handle(ActivateUserCommand command, CancellationToken cancellationToken)
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

        var outranks = await AccessChecks.EnsureOutranksAsync(caller.Value, user.Id, access, cancellationToken);
        if (outranks.IsFailure)
        {
            return outranks.Error; // R11
        }

        user.Activate();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await access.InvalidateAsync(user.Id, cancellationToken);
        return user.ToResponse();
    }
}
