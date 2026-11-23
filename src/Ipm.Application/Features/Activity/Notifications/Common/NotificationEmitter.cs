using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Common;
using Ipm.Domain.Activity;
using Ipm.Domain.Identity;

namespace Ipm.Application.Features.Activity.Notifications.Common;

/// <summary>
/// Spec 08 R11: writes the notifications of one domain event. Everyone is told once; the person who made the change is not told about it,
/// and nobody who cannot see the project is told about something in it. Runs in a scope of its own, after the change was committed.
/// </summary>
internal sealed class NotificationEmitter(IApplicationDbContext db, IUnitOfWork unitOfWork, IAccessService access, IUserContext user, TimeProvider time)
{
    /// <summary>The seed and background work notify nobody.</summary>
    public bool InRequest => user.InRequest;

    public Guid? Actor => user.UserId;

    public DateOnly Today => RiyadhClock.Today(time);

    /// <summary>R11 and R13. <paramref name="projectId"/> null skips the visibility check (someone just removed from a project cannot see it any more).</summary>
    public async Task EmitAsync(
        string kind, IEnumerable<Guid> recipients, string title, string body, string link, Guid? projectId, string subjectType, Guid subjectId, CancellationToken cancellationToken)
    {
        foreach (var recipient in recipients.Distinct().Where(r => r != Actor))
        {
            var recipientAccess = await access.GetAsync(recipient, cancellationToken);
            if (recipientAccess is not { IsActive: true } || (projectId is { } project && !recipientAccess.Can(Permissions.ProjectsView, project)))
            {
                continue;
            }

            db.Notifications.Add(Notification.Create(recipient, kind, title, body, link, time.GetUtcNow(), projectId, subjectType, subjectId, Actor));
        }
    }

    public Task SaveAsync(CancellationToken cancellationToken) => unitOfWork.SaveChangesAsync(cancellationToken);
}
