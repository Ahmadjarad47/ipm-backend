using Ipm.Application.Abstractions.Messaging;

namespace Ipm.Application.Features.Activity.Notifications.Commands.MarkAllNotificationsRead;

/// <summary>Spec 08 R12: marks all the caller own unread notifications as read, or only those of one project.</summary>
public sealed record MarkAllNotificationsReadCommand(Guid? ProjectId) : ICommand;
