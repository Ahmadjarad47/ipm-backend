using Ipm.Application.Abstractions.Data;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Identity.Common;

/// <summary>
/// An in-memory picture of who holds <c>roles.manage</c>, used to answer R13 ("is there still an active user who
/// can manage roles after this change?") without writing the change first. The people and roles of an internal
/// system are few, so loading them all is cheap. Each <c>With…</c> method applies a change to the picture.
/// </summary>
internal sealed class RoleManagerSnapshot
{
    private readonly Dictionary<Guid, (bool Active, List<Guid> RoleIds)> _users;
    private readonly Dictionary<Guid, (RoleScope Scope, List<string> Granted, List<string> Denied)> _roles;

    private RoleManagerSnapshot(
        Dictionary<Guid, (bool Active, List<Guid> RoleIds)> users,
        Dictionary<Guid, (RoleScope Scope, List<string> Granted, List<string> Denied)> roles)
    {
        _users = users;
        _roles = roles;
    }

    public static async Task<RoleManagerSnapshot> LoadAsync(IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var users = await db.Users.AsNoTracking().Select(u => new { u.Id, u.IsActive, u.RoleIds }).ToListAsync(cancellationToken);
        var roles = await db.Roles.AsNoTracking().Select(r => new { r.Id, r.Scope, r.Granted, r.Denied }).ToListAsync(cancellationToken);

        return new RoleManagerSnapshot(
            users.ToDictionary(u => u.Id, u => (u.IsActive, u.RoleIds)),
            roles.ToDictionary(r => r.Id, r => (r.Scope, r.Granted, r.Denied)));
    }

    /// <summary>At least one active user whose system roles grant <c>roles.manage</c> and none of them denies it.</summary>
    public bool HasRoleManager() => _users.Values.Any(u => u.Active && Holds(u.RoleIds));

    public RoleManagerSnapshot WithUserDeactivated(Guid userId)
    {
        if (_users.TryGetValue(userId, out var user))
        {
            _users[userId] = (false, user.RoleIds);
        }

        return this;
    }

    public RoleManagerSnapshot WithUserRoles(Guid userId, IEnumerable<Guid> roleIds)
    {
        if (_users.TryGetValue(userId, out var user))
        {
            _users[userId] = (user.Active, [.. roleIds]);
        }

        return this;
    }

    public RoleManagerSnapshot WithRolePermissions(Guid roleId, IEnumerable<string> granted, IEnumerable<string> denied)
    {
        if (_roles.TryGetValue(roleId, out var role))
        {
            _roles[roleId] = (role.Scope, [.. granted], [.. denied]);
        }

        return this;
    }

    private bool Holds(List<Guid> roleIds)
    {
        var roles = roleIds.Where(_roles.ContainsKey).Select(id => _roles[id]).Where(r => r.Scope == RoleScope.System).ToList();
        return roles.Any(r => r.Granted.Contains(Permissions.RolesManage)) && !roles.Any(r => r.Denied.Contains(Permissions.RolesManage));
    }
}
