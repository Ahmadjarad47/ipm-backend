using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Activity.Notifications.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Activity.Notifications.Queries.GetUnreadNotificationCount;

internal sealed class GetUnreadNotificationCountQueryHandler(IApplicationDbContext db, IUserContext currentUser) : IQueryHandler<GetUnreadNotificationCountQuery, UnreadCount>
{
    public async ValueTask<Result<UnreadCount>> Handle(GetUnreadNotificationCountQuery query, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } me)
        {
            return AuthErrors.Unauthenticated;
        }

        return new UnreadCount(await db.Notifications.AsNoTracking().CountAsync(n => n.UserId == me && n.ReadAt == null, cancellationToken));
    }
}
