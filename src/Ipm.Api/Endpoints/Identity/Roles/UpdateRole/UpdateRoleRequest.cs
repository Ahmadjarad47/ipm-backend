using Ipm.Domain.Identity;

namespace Ipm.Api.Endpoints.Identity.Roles.UpdateRole;

/// <summary><c>scope</c> is optional and only checked: sending a different value is refused (422).</summary>
public sealed record UpdateRoleRequest(
    string Name,
    string? Description,
    IReadOnlyList<string>? Permissions,
    IReadOnlyList<string>? Denied,
    RoleScope? Scope);
