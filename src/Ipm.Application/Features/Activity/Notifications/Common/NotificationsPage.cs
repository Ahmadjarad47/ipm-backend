namespace Ipm.Application.Features.Activity.Notifications.Common;

/// <summary>R12: a page of notifications with the unread count of the user.</summary>
public sealed record NotificationsPage(IReadOnlyList<NotificationResponse> Items, int Total, int Unread, int Page, int PageSize);
