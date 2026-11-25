using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Realtime;
using Ipm.Application.Abstractions.Telegram;
using Ipm.Application.Common;
using Ipm.Domain.Activity;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Activity.Notifications.Common;

/// <summary>
/// Spec 08 R11: writes the notifications of one domain event. Everyone is told once; the person who made the change is not told about it,
/// and nobody who cannot see the project is told about something in it. Runs in a scope of its own, after the change was committed.
/// </summary>
internal sealed class NotificationEmitter(
    IApplicationDbContext db, IUnitOfWork unitOfWork, IAccessService access, IUserContext user, TimeProvider time, INotificationPusher pusher, ITelegramSettings telegram)
{
    private readonly List<Notification> pending = [];

    /// <summary>The seed and background work notify nobody.</summary>
    public bool InRequest => user.InRequest;

    public Guid? Actor => user.UserId;

    public DateOnly Today => RiyadhClock.Today(time);

    /// <summary>R11 and R13. <paramref name="projectId"/> null skips the visibility check (someone just removed from a project cannot see it any more).</summary>
    public async Task EmitAsync(
        string kind, IEnumerable<Guid> recipients, string title, string body, string link, Guid? projectId, string subjectType, Guid subjectId, CancellationToken cancellationToken)
    {
        var created = new List<Notification>();
        foreach (var recipient in recipients.Distinct().Where(r => r != Actor))
        {
            var recipientAccess = await access.GetAsync(recipient, cancellationToken);
            if (recipientAccess is not { IsActive: true } || (projectId is { } project && !recipientAccess.Can(Permissions.ProjectsView, project)))
            {
                continue;
            }

            var notification = Notification.Create(recipient, kind, title, body, link, time.GetUtcNow(), projectId, subjectType, subjectId, Actor);
            db.Notifications.Add(notification);
            created.Add(notification);
            pending.Add(notification);
        }

        await QueueForTelegramAsync(created, cancellationToken);
    }

    /// <summary>
    /// Spec 11 R6: the second channel. For the kinds that go to Telegram, every recipient with an active link also gets an outbox row, added to the
    /// same change set so it is committed together with the notification (all or nothing). One query finds the links of the whole batch.
    /// </summary>
    private async Task QueueForTelegramAsync(List<Notification> created, CancellationToken cancellationToken)
    {
        if (!telegram.Enabled || created.Count == 0 || !NotificationKinds.TelegramDefault.Contains(created[0].Kind))
        {
            return;
        }

        var userIds = created.Select(n => n.UserId).ToList();
        var chats = await db.TelegramLinks.AsNoTracking()
            .Where(l => userIds.Contains(l.UserId) && l.DisabledAt == null)
            .ToDictionaryAsync(l => l.UserId, l => l.ChatId, cancellationToken);

        foreach (var notification in created.Where(n => chats.ContainsKey(n.UserId)))
        {
            var url = TelegramMessageFormatter.AbsoluteUrl(telegram.AppBaseUrl, notification.Link);
            db.TelegramOutbox.Add(TelegramMessage.Create(
                notification.Id, notification.UserId, chats[notification.UserId], TelegramMessageFormatter.Format(notification.Title, notification.Body, url), url, notification.CreatedAt));
        }
    }

    /// <summary>
    /// Commits the batch, then — only once the rows actually exist — pushes each one to its recipient over SignalR,
    /// if they have a live connection. A push never runs before the commit, and a push failure is swallowed by
    /// <see cref="INotificationPusher"/> itself, so it can never undo or fail the request that raised the notification.
    /// </summary>
    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        await unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var notification in pending)
        {
            await pusher.PushAsync(notification.UserId, notification.ToResponse(), cancellationToken);
        }

        pending.Clear();
    }
}
