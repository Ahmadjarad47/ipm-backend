using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Identity.Users.Common;

/// <summary>Checks that need the database, shared by several user commands.</summary>
internal static class UserChecks
{
    /// <summary>R19 and R12 for a set of roles to assign: they exist, are system roles, and the caller holds every permission they give.</summary>
    public static async Task<Result> EnsureAssignableAsync(
        IApplicationDbContext db, UserAccess caller, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken)
    {
        var distinct = roleIds.Distinct().ToList();
        var roles = await db.Roles.AsNoTracking().Where(r => distinct.Contains(r.Id)).ToListAsync(cancellationToken);
        if (roles.Count != distinct.Count)
        {
            return UserErrors.RoleNotFound;
        }

        if (roles.Any(r => r.Scope != RoleScope.System))
        {
            return UserErrors.RoleScopeInvalid;
        }

        var missing = roles.SelectMany(r => r.EffectivePermissions).Where(p => !caller.Effective.Contains(p)).Distinct().ToList();
        return missing.Count == 0 ? Result.Success() : RoleErrors.PermissionNotGrantable(missing);
    }

    /// <summary>R14: the manager is an active user, not the user themself, and the reporting line has no cycle.</summary>
    public static async Task<Result> EnsureManagerAsync(
        IApplicationDbContext db, Guid? managerId, Guid? userId, CancellationToken cancellationToken)
    {
        if (managerId is not { } manager)
        {
            return Result.Success();
        }

        if (manager == userId)
        {
            return UserErrors.ManagerInvalid("لا يمكن أن يكون المستخدم مديراً لنفسه.");
        }

        var lines = await db.Users.AsNoTracking().Select(u => new { u.Id, u.IsActive, u.ManagerId }).ToListAsync(cancellationToken);
        var managerLine = lines.SingleOrDefault(u => u.Id == manager);
        if (managerLine is not { IsActive: true })
        {
            return UserErrors.ManagerInvalid("المدير المباشر يجب أن يكون مستخدماً نشطاً.");
        }

        // Walk up from the new manager: reaching the user again would close a loop.
        var parents = lines.ToDictionary(u => u.Id, u => u.ManagerId);
        var seen = new HashSet<Guid>();
        for (Guid? cursor = manager; cursor is { } id; cursor = parents.GetValueOrDefault(id))
        {
            if (id == userId || !seen.Add(id))
            {
                return UserErrors.ManagerInvalid("سلسلة المدراء تُنشئ حلقة دائرية.");
            }
        }

        return Result.Success();
    }
}
