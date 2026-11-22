namespace Ipm.Application.Features.Projects.Projects.Common;

/// <summary>A member of a project and their project-scope role.</summary>
public sealed record ProjectMemberResponse(Guid UserId, Guid RoleId);
