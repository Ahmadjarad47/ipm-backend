namespace Ipm.Api.Endpoints.Activity.Notifications.MarkNotificationsRead;

internal sealed record MarkNotificationsReadRequest(IReadOnlyList<Guid> Ids);
