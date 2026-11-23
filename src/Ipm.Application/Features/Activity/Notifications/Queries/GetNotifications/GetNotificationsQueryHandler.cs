using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Activity.Notifications.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Activity.Notifications.Queries.GetNotifications;

internal sealed class GetNotificationsQueryHandler(IApplicationDbContext db, IUserContext currentUser) : IQueryHandler<GetNotificationsQuery, NotificationsPage>
{
    public async ValueTask<Result<NotificationsPage>> Handle(GetNotificationsQuery query, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } me)
        {
            return AuthErrors.Unauthenticated;
        }

        var mine = db.Notifications.AsNoTracking().Where(n => n.UserId == me);
        var unread = await mine.CountAsync(n => n.ReadAt == null, cancellationToken);
        var shown = query.UnreadOnly ? mine.Where(n => n.ReadAt == null) : mine;
        var total = await shown.CountAsync(cancellationToken);
        var rows = await shown.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).Skip(query.Page.Skip).Take(query.Page.Take).ToListAsync(cancellationToken);
        return new NotificationsPage([.. rows.Select(n => n.ToResponse())], total, unread, Math.Max(query.Page.Page, 1), query.Page.Take);
    }
}
