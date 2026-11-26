using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Identity.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Identity.Users.Commands.UnlockUser;

internal sealed class UnlockUserCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, IAccessService access)
    : ICommandHandler<UnlockUserCommand>
{
    public async ValueTask<Result> Handle(UnlockUserCommand command, CancellationToken cancellationToken)
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

        var outranks = await AccessChecks.EnsureOutranksAsync(caller.Value, user.Id, access, cancellationToken);
        if (outranks.IsFailure)
        {
            return outranks.Error; // R11
        }

        // Idempotent: unlocking an account that is not locked just clears the failed-attempt counter.
        user.LockoutEnd = null;
        user.AccessFailedCount = 0;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
