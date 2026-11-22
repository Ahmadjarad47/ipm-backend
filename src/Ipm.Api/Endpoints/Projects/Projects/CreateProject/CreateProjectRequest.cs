namespace Ipm.Api.Endpoints.Projects.Projects.CreateProject;

public sealed record CreateProjectRequest(
    string Code,
    string Name,
    string? Description,
    Guid OwnerId,
    DateOnly StartDate,
    DateOnly EndDate,
    Guid? TemplateId);
