using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Activity.Notifications.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Activity.Notifications.Commands.MarkNotificationRead;

internal sealed class MarkNotificationReadCommandHandler(IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, TimeProvider time)
    : ICommandHandler<MarkNotificationReadCommand, NotificationResponse>
{
    public async ValueTask<Result<NotificationResponse>> Handle(MarkNotificationReadCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } me)
        {
            return AuthErrors.Unauthenticated;
        }

        var notification = await db.Notifications.SingleOrDefaultAsync(n => n.Id == command.Id && n.UserId == me, cancellationToken);
        if (notification is null)
        {
            return Error.NotFound("Notification.NotFound", "الإشعار غير موجود.");
        }

        notification.MarkRead(time.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return notification.ToResponse();
    }
}
