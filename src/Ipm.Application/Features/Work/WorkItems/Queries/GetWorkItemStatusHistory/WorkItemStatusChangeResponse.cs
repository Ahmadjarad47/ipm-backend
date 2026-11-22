namespace Ipm.Application.Features.Work.WorkItems.Queries.GetWorkItemStatusHistory;

/// <summary>One executed status change of a work item. <see cref="ApprovalOverridden"/> marks a move that needed an approval and was made with <c>audit.override</c>.</summary>
public sealed record WorkItemStatusChangeResponse(
    DateTimeOffset At,
    Guid FromStatusId,
    Guid ToStatusId,
    Guid ActorId,
    string ActorName,
    string? Comment,
    bool ApprovalOverridden);
