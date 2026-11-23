using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Activity.Notifications.Commands.MarkAllNotificationsRead;

internal sealed class MarkAllNotificationsReadCommandHandler(IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, TimeProvider time)
    : ICommandHandler<MarkAllNotificationsReadCommand>
{
    public async ValueTask<Result> Handle(MarkAllNotificationsReadCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } me)
        {
            return AuthErrors.Unauthenticated;
        }

        var now = time.GetUtcNow();
        var unread = db.Notifications.Where(n => n.UserId == me && n.ReadAt == null);
        if (command.ProjectId is { } projectId)
        {
            unread = unread.Where(n => n.ProjectId == projectId);
        }

        foreach (var notification in await unread.ToListAsync(cancellationToken))
        {
            notification.MarkRead(now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
