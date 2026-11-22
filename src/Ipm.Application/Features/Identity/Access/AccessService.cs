using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Caching;
using Ipm.Application.Abstractions.Data;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Identity.Access;

internal sealed class AccessService(IApplicationDbContext db, ICacheService cache) : IAccessService
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private static string Key(Guid userId) => $"perm:{userId}";

    public async Task<UserAccess?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var cached = await cache.GetAsync<UserAccess>(Key(userId), cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var user = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Id, u.IsActive, u.SecurityStamp, u.MustChangePassword, u.RoleIds })
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return null;
        }

        // Project roles are granted inside a project (part 04), never system-wide.
        var roles = await db.Roles
            .AsNoTracking()
            .Where(r => user.RoleIds.Contains(r.Id) && r.Scope == RoleScope.System)
            .Select(r => new { r.Granted, r.Denied })
            .ToListAsync(cancellationToken);

        var granted = roles.SelectMany(r => r.Granted).ToHashSet();
        var denied = roles.SelectMany(r => r.Denied).ToHashSet();

        var access = new UserAccess(
            user.Id,
            user.IsActive,
            user.SecurityStamp ?? string.Empty,
            user.MustChangePassword,
            [.. Permissions.All.Where(granted.Contains)],
            [.. Permissions.All.Where(denied.Contains)]);

        await cache.SetAsync(Key(userId), access, Lifetime, cancellationToken);
        return access;
    }

    public async Task<bool> CanAsync(Guid userId, string permission, Guid? projectId = null, CancellationToken cancellationToken = default)
    {
        var access = await GetAsync(userId, cancellationToken);
        return access is not null && access.Has(permission);
    }

    public Task InvalidateAsync(Guid userId, CancellationToken cancellationToken = default) =>
        cache.RemoveAsync(Key(userId), cancellationToken);

    public async Task InvalidateRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        var holders = await db.Users.Where(u => u.RoleIds.Contains(roleId)).Select(u => u.Id).ToListAsync(cancellationToken);
        foreach (var id in holders)
        {
            await InvalidateAsync(id, cancellationToken);
        }
    }
}
