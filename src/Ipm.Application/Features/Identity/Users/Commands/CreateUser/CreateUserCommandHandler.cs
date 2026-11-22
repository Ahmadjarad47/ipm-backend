using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Identity.Common;
using Ipm.Application.Features.Identity.Users.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Identity.Users.Commands.CreateUser;

internal sealed class CreateUserCommandHandler(
    UserManager<User> users, IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, IAccessService access)
    : ICommandHandler<CreateUserCommand, UserResponse>
{
    public async ValueTask<Result<UserResponse>> Handle(CreateUserCommand command, CancellationToken cancellationToken)
    {
        var caller = await AccessChecks.CallerAsync(currentUser, access, cancellationToken);
        if (caller.IsFailure)
        {
            return caller.Error;
        }

        var username = command.Username.Trim().ToLowerInvariant();
        var normalizedUsername = users.NormalizeName(username);
        if (await db.Users.AnyAsync(u => u.NormalizedUserName == normalizedUsername, cancellationToken))
        {
            return UserErrors.UsernameTaken; // R7
        }

        var normalizedEmail = users.NormalizeEmail(command.Email.Trim());
        if (await db.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken))
        {
            return UserErrors.EmailTaken; // R7
        }

        var assignable = await UserChecks.EnsureAssignableAsync(db, caller.Value, command.SystemRoleIds, cancellationToken);
        if (assignable.IsFailure)
        {
            return assignable.Error; // R12, R19
        }

        var manager = await UserChecks.EnsureManagerAsync(db, command.ManagerId, userId: null, cancellationToken);
        if (manager.IsFailure)
        {
            return manager.Error; // R14
        }

        var user = User.Create(
            username, normalizedUsername, command.Name, command.Email, normalizedEmail, command.Title, command.ManagerId, command.SystemRoleIds);
        var password = await Passwords.SetAsync(users, user, command.Password);
        if (password.IsFailure)
        {
            return password.Error; // R5
        }

        user.PasswordSetByAdministrator(); // R6
        db.Users.Add(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return user.ToResponse();
    }
}
