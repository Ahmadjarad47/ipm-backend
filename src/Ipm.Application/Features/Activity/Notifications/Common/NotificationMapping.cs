using Ipm.Domain.Activity;

namespace Ipm.Application.Features.Activity.Notifications.Common;

internal static class NotificationMapping
{
    public static NotificationResponse ToResponse(this Notification n) =>
        new(n.Id, n.Kind, n.Title, n.Body, n.Link, n.ProjectId, n.ActorId, n.IsRead, n.ReadAt, n.CreatedAt);
}
