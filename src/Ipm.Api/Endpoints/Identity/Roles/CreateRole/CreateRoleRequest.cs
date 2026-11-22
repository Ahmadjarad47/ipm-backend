using Ipm.Domain.Identity;

namespace Ipm.Api.Endpoints.Identity.Roles.CreateRole;

/// <summary>When both lists are empty and <c>copyFromRoleId</c> is given, the role starts as a copy of that role.</summary>
public sealed record CreateRoleRequest(
    string Name,
    string? Description,
    RoleScope Scope,
    IReadOnlyList<string>? Permissions,
    IReadOnlyList<string>? Denied,
    Guid? CopyFromRoleId);
