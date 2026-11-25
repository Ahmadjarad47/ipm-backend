using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Ipm.RealTime.Hubs;

/// <summary>
/// Push-only channel for in-app notifications (spec 08 R11). The server never receives calls on this hub —
/// it only sends <c>notificationReceived</c> to a signed-in user's connections, via <see cref="Authentication.SubClaimUserIdProvider"/>,
/// once a notification row has actually been committed (see <see cref="Notifications.SignalRNotificationPusher"/>).
/// </summary>
[Authorize]
public sealed class NotificationsHub : Hub;
