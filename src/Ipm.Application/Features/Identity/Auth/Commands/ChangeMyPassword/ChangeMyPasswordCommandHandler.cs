using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Identity.Auth.Common;
using Ipm.Application.Features.Identity.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Identity.Auth.Commands.ChangeMyPassword;

internal sealed class ChangeMyPasswordCommandHandler(
    UserManager<User> users,
    IApplicationDbContext db,
    IUnitOfWork unitOfWork,
    IUserContext currentUser,
    ITokenService tokens,
    IAccessService access,
    ISecurityEventLog securityLog) : ICommandHandler<ChangeMyPasswordCommand, SessionResult>
{
    public async ValueTask<Result<SessionResult>> Handle(ChangeMyPasswordCommand command, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == currentUser.UserId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        if (!await users.CheckPasswordAsync(user, command.CurrentPassword))
        {
            return AuthErrors.WrongCurrentPassword;
        }

        var set = await Passwords.SetAsync(users, user, command.NewPassword);
        if (set.IsFailure)
        {
            return set.Error;
        }

        user.PasswordChangedByOwner();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await tokens.RevokeAllAsync(user.Id, cancellationToken);
        await access.InvalidateAsync(user.Id, cancellationToken);
        await securityLog.LogAsync(SecurityEventType.PasswordChanged, user.Id, user.UserName, cancellationToken: cancellationToken);
        return new SessionResult(await tokens.IssueAsync(user, cancellationToken), MustChangePassword: false);
    }
}
