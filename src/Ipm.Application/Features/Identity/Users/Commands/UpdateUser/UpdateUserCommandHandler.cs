using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Identity.Users.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Identity.Users.Commands.UpdateUser;

internal sealed class UpdateUserCommandHandler(UserManager<User> users, IApplicationDbContext db, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateUserCommand, UserResponse>
{
    public async ValueTask<Result<UserResponse>> Handle(UpdateUserCommand command, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == command.Id, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        if (!user.HasVersion(command.ExpectedVersion))
        {
            return Error.StaleVersion;
        }

        var normalizedEmail = users.NormalizeEmail(command.Email.Trim());
        if (await db.Users.AnyAsync(u => u.Id != user.Id && u.NormalizedEmail == normalizedEmail, cancellationToken))
        {
            return UserErrors.EmailTaken; // R7
        }

        var manager = await UserChecks.EnsureManagerAsync(db, command.ManagerId, user.Id, cancellationToken);
        if (manager.IsFailure)
        {
            return manager.Error; // R14
        }

        user.UpdateProfile(command.Name, command.Email, normalizedEmail, command.Title, command.ManagerId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return user.ToResponse();
    }
}
