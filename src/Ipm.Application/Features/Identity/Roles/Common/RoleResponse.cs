namespace Ipm.Application.Features.Identity.Roles.Common;

/// <summary>One role with its permissions. <see cref="Version"/> is sent as the ETag.</summary>
public sealed record RoleResponse(
    Guid Id,
    string Key,
    string Name,
    string Description,
    string Scope,
    bool IsBuiltIn,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> Denied,
    uint Version);
