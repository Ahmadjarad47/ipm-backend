namespace Ipm.Domain.Activity;

/// <summary>
/// One notification for one user (spec 08 R11, R13). A record, not an audited entity: it is written once and only ever marked as read.
/// The text is made by the server in Arabic, the link is a relative path built from ids, never from user text.
/// </summary>
public sealed class Notification
{
    public const int TitleMaxLength = 200;
    public const int BodyMaxLength = 500;

    private Notification() { }

    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid UserId { get; private init; }

    public string Kind { get; private init; } = string.Empty;

    public string Title { get; private init; } = string.Empty;

    public string Body { get; private init; } = string.Empty;

    public string Link { get; private init; } = string.Empty;

    public Guid? ProjectId { get; private init; }

    public string? SubjectType { get; private init; }

    public Guid? SubjectId { get; private init; }

    public Guid? ActorId { get; private init; }

    public DateTimeOffset? ReadAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private init; }

    public bool IsRead => ReadAt is not null;

    public static Notification Create(
        Guid userId, string kind, string title, string body, string link, DateTimeOffset at,
        Guid? projectId = null, string? subjectType = null, Guid? subjectId = null, Guid? actorId = null, Guid? id = null, DateTimeOffset? readAt = null) =>
        new()
        {
            Id = id ?? Guid.CreateVersion7(),
            UserId = userId,
            Kind = kind,
            Title = Clean(title, TitleMaxLength),
            Body = Clean(body, BodyMaxLength),
            Link = link,
            ProjectId = projectId,
            SubjectType = subjectType,
            SubjectId = subjectId,
            ActorId = actorId,
            CreatedAt = at,
            ReadAt = readAt,
        };

    /// <summary>R12: reading twice keeps the first time.</summary>
    public void MarkRead(DateTimeOffset at) => ReadAt ??= at;

    /// <summary>R13: control characters out, length capped.</summary>
    public static string Clean(string? text, int max)
    {
        var clean = new string((text ?? string.Empty).Where(c => !char.IsControl(c) || c is '\n').ToArray()).Trim();
        return clean.Length > max ? clean[..max] : clean;
    }
}
