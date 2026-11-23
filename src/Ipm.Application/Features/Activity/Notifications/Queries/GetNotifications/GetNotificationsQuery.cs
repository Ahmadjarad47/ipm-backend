using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Features.Activity.Notifications.Common;

namespace Ipm.Application.Features.Activity.Notifications.Queries.GetNotifications;

/// <summary>Spec 08 R12: the signed-in user notifications, newest first. Those of others are never visible.</summary>
public sealed record GetNotificationsQuery(bool UnreadOnly, PageRequest Page) : IQuery<NotificationsPage>;
