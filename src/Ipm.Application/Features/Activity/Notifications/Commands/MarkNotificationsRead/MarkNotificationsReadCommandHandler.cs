using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Activity.Notifications.Commands.MarkNotificationsRead;

internal sealed class MarkNotificationsReadCommandHandler(IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, TimeProvider time)
    : ICommandHandler<MarkNotificationsReadCommand>
{
    public async ValueTask<Result> Handle(MarkNotificationsReadCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } me)
        {
            return AuthErrors.Unauthenticated;
        }

        if (command.Ids.Count == 0)
        {
            return Result.Success();
        }

        var ids = command.Ids.Distinct().ToArray();
        var now = time.GetUtcNow();
        var unread = await db.Notifications.Where(n => n.UserId == me && ids.Contains(n.Id) && n.ReadAt == null).ToListAsync(cancellationToken);

        foreach (var notification in unread)
        {
            notification.MarkRead(now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
