using Ipm.Application.Abstractions.Messaging;

namespace Ipm.Application.Features.Activity.Notifications.Commands.MarkNotificationsRead;

/// <summary>
/// Spec 08 R12: marks several of the caller's own notifications as read in one call — the front end batches the
/// "seen in view" ids instead of one request per row. Ids of other users, or already read, are silently skipped;
/// an id that does not exist is not an error either (reading twice, or reading something since deleted, is harmless).
/// </summary>
public sealed record MarkNotificationsReadCommand(IReadOnlyList<Guid> Ids) : ICommand;
