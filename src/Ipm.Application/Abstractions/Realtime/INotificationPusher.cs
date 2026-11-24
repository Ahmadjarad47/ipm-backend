using Ipm.Application.Features.Activity.Notifications.Common;

namespace Ipm.Application.Abstractions.Realtime;

/// <summary>
/// Pushes an already-committed notification to its recipient's live connections (SignalR). Implementations must
/// never be called before the row is saved, and a delivery failure must never fail or roll back the request that
/// raised it — the caller treats this the same as any other best-effort reaction.
/// </summary>
public interface INotificationPusher
{
    Task PushAsync(Guid userId, NotificationResponse notification, CancellationToken cancellationToken);
}
