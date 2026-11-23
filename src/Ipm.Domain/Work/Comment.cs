using Ipm.Domain.Common;

namespace Ipm.Domain.Work;

/// <summary>
/// A comment on a project or a work item (FR-COL). The text history and the @mentions are columns of the comment itself, so editing it
/// moves its version (ETag) and shows in audit_log. Never removed: deleting marks it, and readers get it without text (R15).
/// </summary>
public sealed class Comment : AuditableEntity, IAuditSubject
{
    Guid? IAuditSubject.AuditProjectId => SubjectType == CommentSubjectType.Project ? SubjectId : null;

    Guid? IAuditSubject.AuditWorkItemId => SubjectType == CommentSubjectType.WorkItem ? SubjectId : null;


    public const int BodyMaxLength = 5000;

    private Comment() { }

    private Comment(Guid id) : base(id) { }

    public CommentSubjectType SubjectType { get; private set; }

    public Guid SubjectId { get; private set; }

    public Guid AuthorId { get; private set; }

    public string Body { get; private set; } = string.Empty;

    public bool IsEdited { get; private set; }

    public bool IsDeleted { get; private set; }

    public List<CommentRevision> Revisions { get; private set; } = [];

    public List<Guid> MentionedUserIds { get; private set; } = [];

    /// <summary>R13: the text is the first revision. The handler checks the subject, the permission and that the project is writable. <paramref name="id"/> is only given by the seed.</summary>
    public static Comment Create(
        CommentSubjectType subjectType, Guid subjectId, Guid authorId, string body, IEnumerable<Guid> mentionedUserIds, DateTimeOffset at, Guid? id = null)
    {
        var text = body.Trim();
        var comment = id is { } fixedId ? new Comment(fixedId) : new Comment();
        comment.SubjectType = subjectType;
        comment.SubjectId = subjectId;
        comment.AuthorId = authorId;
        comment.Body = text;
        comment.MentionedUserIds = [.. mentionedUserIds.Distinct()];
        comment.Revisions = [new CommentRevision(text, at)];
        if (comment.MentionedUserIds.Count > 0)
        {
            comment.RaiseDomainEvent(new Ipm.Domain.Activity.Events.CommentMentioned(comment.Id, subjectType, subjectId, comment.MentionedUserIds, Preview(text)));
        }

        return comment;
    }

    /// <summary>R14: only the author, inside the window, and not once deleted. The edit is appended to the history.</summary>
    public Result Edit(Guid editorId, string body, IEnumerable<Guid> mentionedUserIds, DateTimeOffset now, int windowHours)
    {
        if (IsDeleted)
        {
            return CommentErrors.Deleted;
        }

        if (editorId != AuthorId)
        {
            return CommentErrors.NotAuthor;
        }

        if (now - CreatedAt > TimeSpan.FromHours(windowHours))
        {
            return CommentErrors.EditWindowExpired(windowHours);
        }

        var text = body.Trim();
        Body = text;
        IsEdited = true;
        var previous = MentionedUserIds;
        MentionedUserIds = [.. mentionedUserIds.Distinct()];
        var added = MentionedUserIds.Where(u => !previous.Contains(u)).ToList();
        if (added.Count > 0)
        {
            RaiseDomainEvent(new Ipm.Domain.Activity.Events.CommentMentioned(Id, SubjectType, SubjectId, added, Preview(text)));
        }

        Revisions = [.. Revisions, new CommentRevision(text, now)];
        return Result.Success();
    }

    private static string Preview(string text) => text.Length > 80 ? text[..80] : text;

    /// <summary>R15: the permission rule (the author, or files.delete) is checked by the handler.</summary>
    public Result Delete()
    {
        if (IsDeleted)
        {
            return CommentErrors.Deleted;
        }

        IsDeleted = true;
        return Result.Success();
    }
}
