using Microsoft.AspNetCore.Authorization;

namespace Ipm.Infrastructure.Authentication;

/// <summary>Requires one permission key, e.g. <c>.RequireAuthorization(Permissions.RolesManage)</c>.</summary>
public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
