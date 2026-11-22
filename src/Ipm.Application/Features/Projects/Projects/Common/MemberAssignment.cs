namespace Ipm.Application.Features.Projects.Projects.Common;

/// <summary>One entry of the member list of a project: a user and their project-scope role.</summary>
public sealed record MemberAssignment(Guid UserId, Guid RoleId);
