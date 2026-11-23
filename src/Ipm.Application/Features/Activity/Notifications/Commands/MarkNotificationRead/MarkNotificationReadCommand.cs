using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Activity.Notifications.Common;

namespace Ipm.Application.Features.Activity.Notifications.Commands.MarkNotificationRead;

/// <summary>Spec 08 R12: marks one of the caller own notifications as read. Again is harmless; a notification of someone else is a 404.</summary>
public sealed record MarkNotificationReadCommand(Guid Id) : ICommand<NotificationResponse>;
