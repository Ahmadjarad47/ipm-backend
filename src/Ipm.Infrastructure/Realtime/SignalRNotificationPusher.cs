using Ipm.Application.Abstractions.Realtime;
using Ipm.Application.Features.Activity.Notifications.Common;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Ipm.Infrastructure.Realtime;

/// <summary>
/// Sends an already-committed notification to the recipient's live connections, if any are open. A transport
/// failure (hub down, user not connected) is logged and swallowed: nothing here may fail the request that
/// raised the notification — the REST poll/refresh path always stays correct regardless of push delivery.
/// </summary>
internal sealed class SignalRNotificationPusher(IHubContext<NotificationsHub> hub, ILogger<SignalRNotificationPusher> logger) : INotificationPusher
{
    private const string Event = "notificationReceived";

    public async Task PushAsync(Guid userId, NotificationResponse notification, CancellationToken cancellationToken)
    {
        try
        {
            await hub.Clients.User(userId.ToString()).SendAsync(Event, notification, cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogError(e, "Failed to push notification {NotificationId} to user {UserId}", notification.Id, userId);
        }
    }
}
