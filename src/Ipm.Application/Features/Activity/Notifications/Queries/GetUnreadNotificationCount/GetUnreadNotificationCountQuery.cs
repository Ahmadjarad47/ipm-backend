using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Activity.Notifications.Common;

namespace Ipm.Application.Features.Activity.Notifications.Queries.GetUnreadNotificationCount;

/// <summary>Spec 08 R12: the badge.</summary>
public sealed record GetUnreadNotificationCountQuery : IQuery<UnreadCount>;
