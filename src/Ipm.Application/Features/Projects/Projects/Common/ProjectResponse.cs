namespace Ipm.Application.Features.Projects.Projects.Common;

/// <summary>
/// One project. <see cref="Version"/> is sent as the ETag and moves with every change of the project, its members included.
/// <see cref="WorkflowOverrides"/> maps a work item type to the workflow this project uses for it instead of the type own.
/// </summary>
public sealed record ProjectResponse(
    Guid Id,
    string Code,
    string Name,
    string Description,
    Guid OwnerId,
    Guid StatusId,
    DateOnly StartDate,
    DateOnly EndDate,
    bool Archived,
    IReadOnlyList<ProjectMemberResponse> Members,
    IReadOnlyList<Guid> AllowedTypeIds,
    Guid DefaultTypeId,
    IReadOnlyDictionary<Guid, Guid> WorkflowOverrides,
    uint Version,
    ProjectProgress? Progress = null,
    int WorkItemCount = 0,
    int OverdueCount = 0,
    string? RefPattern = null);
