namespace Ipm.Api.Endpoints.Projects.Projects.UpdateProject;

/// <summary><c>code</c> and <c>statusId</c> are optional and only checked (a different value is refused). An omitted <c>ownerId</c> keeps the owner.</summary>
public sealed record UpdateProjectRequest(
    string Name,
    string? Description,
    DateOnly StartDate,
    DateOnly EndDate,
    Guid? OwnerId,
    string? Code,
    Guid? StatusId);
