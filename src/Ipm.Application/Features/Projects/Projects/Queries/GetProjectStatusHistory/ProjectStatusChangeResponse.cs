namespace Ipm.Application.Features.Projects.Projects.Queries.GetProjectStatusHistory;

/// <summary>One executed status change of a project. <see cref="ApprovalOverridden"/> marks a move that needed an approval and was made with <c>audit.override</c>.</summary>
public sealed record ProjectStatusChangeResponse(
    DateTimeOffset At,
    Guid FromStatusId,
    Guid ToStatusId,
    Guid ActorId,
    string ActorName,
    string? Comment,
    bool ApprovalOverridden);
