namespace Ipm.Domain.Work;

/// <summary>
/// One executed status change of a work item (FR-WF-08). Append only, so it is not an <c>Entity</c>.
/// <see cref="ApprovalOverridden"/> marks a move that needed an approval and was made with <c>audit.override</c> instead (R8).
/// </summary>
public sealed class WorkItemStatusChange
{
    public const int CommentMaxLength = 1000;

    private WorkItemStatusChange() { }

    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid WorkItemId { get; private init; }

    public Guid FromStatusId { get; private init; }

    public Guid ToStatusId { get; private init; }

    public Guid ActorId { get; private init; }

    public string? Comment { get; private init; }

    public DateTimeOffset At { get; private init; }

    public bool ApprovalOverridden { get; private init; }

    public static WorkItemStatusChange Create(
        Guid workItemId, Guid fromStatusId, Guid toStatusId, Guid actorId, string? comment, DateTimeOffset at, bool approvalOverridden) =>
        new()
        {
            WorkItemId = workItemId,
            FromStatusId = fromStatusId,
            ToStatusId = toStatusId,
            ActorId = actorId,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            At = at,
            ApprovalOverridden = approvalOverridden,
        };
}
