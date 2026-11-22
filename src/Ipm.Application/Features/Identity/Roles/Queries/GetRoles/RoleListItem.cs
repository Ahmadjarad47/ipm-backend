namespace Ipm.Application.Features.Identity.Roles.Queries.GetRoles;

public sealed record RoleListItem(
    Guid Id,
    string Key,
    string Name,
    string Description,
    string Scope,
    bool IsBuiltIn,
    int PermissionCount,
    int UserCount);
