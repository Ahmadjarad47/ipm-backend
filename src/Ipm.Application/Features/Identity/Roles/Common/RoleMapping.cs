using System.Text.Json;
using Ipm.Domain.Identity;

namespace Ipm.Application.Features.Identity.Roles.Common;

internal static class RoleMapping
{
    public static RoleResponse ToResponse(this Role role) => new(
        role.Id,
        role.Key,
        role.Name,
        role.Description,
        ScopeText(role.Scope),
        role.IsBuiltIn,
        role.Granted,
        role.Denied,
        role.Version);

    /// <summary>The same kebab-case text the JSON, the database and query strings use.</summary>
    public static string ScopeText(RoleScope scope) => JsonNamingPolicy.KebabCaseLower.ConvertName(scope.ToString());
}
