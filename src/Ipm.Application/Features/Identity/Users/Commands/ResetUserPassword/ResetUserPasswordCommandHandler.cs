using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Identity.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Identity.Users.Commands.ResetUserPassword;

internal sealed class ResetUserPasswordCommandHandler(
    UserManager<User> users,
    IApplicationDbContext db,
    IUnitOfWork unitOfWork,
    IUserContext currentUser,
    ITokenService tokens,
    IAccessService access,
    ISecurityEventLog securityLog) : ICommandHandler<ResetUserPasswordCommand>
{
    public async ValueTask<Result> Handle(ResetUserPasswordCommand command, CancellationToken cancellationToken)
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

        var set = await Passwords.SetAsync(users, user, command.NewPassword);
        if (set.IsFailure)
        {
            return set.Error; // R5
        }

        user.PasswordSetByAdministrator(); // R6
        user.LockoutEnd = null;
        user.AccessFailedCount = 0;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await tokens.RevokeAllAsync(user.Id, cancellationToken);
        await access.InvalidateAsync(user.Id, cancellationToken);
        await securityLog.LogAsync(SecurityEventType.PasswordReset, user.Id, user.UserName, "by-administrator", cancellationToken: cancellationToken);
        return Result.Success();
    }
}
