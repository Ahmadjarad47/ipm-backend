namespace Ipm.Api.Endpoints.Identity.Users.SetUserRoles;

public sealed record SetUserRolesRequest(IReadOnlyList<Guid>? SystemRoleIds);
