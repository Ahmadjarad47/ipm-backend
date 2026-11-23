namespace Ipm.Application.Features.Activity.Notifications.Common;

/// <summary>One notification of the signed-in user. <see cref="Link"/> is a relative path of the front end.</summary>
public sealed record NotificationResponse(
    Guid Id,
    string Kind,
    string Title,
    string Body,
    string Link,
    Guid? ProjectId,
    Guid? ActorId,
    bool Read,
    DateTimeOffset? ReadAt,
    DateTimeOffset CreatedAt);
