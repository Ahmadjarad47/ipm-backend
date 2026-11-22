namespace Ipm.Api.Endpoints.Projects.Projects.TransitionProject;

public sealed record TransitionProjectRequest(Guid ToStatusId, string? Comment);
