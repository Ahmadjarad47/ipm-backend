namespace Ipm.Domain.Projects;

/// <summary>
/// One executed status change of a project (FR-WF-08): who moved it from where to where, when, and why. Append only (never updated
/// or deleted), so it is not an <c>Entity</c>. <see cref="ApprovalOverridden"/> marks a move that needed an approval and was made
/// with <c>audit.override</c> instead (R11).
/// </summary>
public sealed class ProjectStatusChange
{
    public const int CommentMaxLength = 1000;

    private ProjectStatusChange() { }

    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid ProjectId { get; private init; }

    public Guid FromStatusId { get; private init; }

    public Guid ToStatusId { get; private init; }

    public Guid ActorId { get; private init; }

    public string? Comment { get; private init; }

    public DateTimeOffset At { get; private init; }

    public bool ApprovalOverridden { get; private init; }

    public static ProjectStatusChange Create(
        Guid projectId, Guid fromStatusId, Guid toStatusId, Guid actorId, string? comment, DateTimeOffset at, bool approvalOverridden) =>
        new()
        {
            ProjectId = projectId,
            FromStatusId = fromStatusId,
            ToStatusId = toStatusId,
            ActorId = actorId,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            At = at,
            ApprovalOverridden = approvalOverridden,
        };
}
