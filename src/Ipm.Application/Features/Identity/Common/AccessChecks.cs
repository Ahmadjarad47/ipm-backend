using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;

namespace Ipm.Application.Features.Identity.Common;

/// <summary>The privilege rules every administration command shares: R11, R12 and R13.</summary>
internal static class AccessChecks
{
    /// <summary>The signed-in user current access, or an unauthenticated error (only possible for a deleted or foreign token).</summary>
    public static async Task<Result<UserAccess>> CallerAsync(IUserContext user, IAccessService access, CancellationToken cancellationToken)
    {
        var current = user.UserId is { } id ? await access.GetAsync(id, cancellationToken) : null;
        if (current is null)
        {
            return AuthErrors.Unauthenticated;
        }

        return current;
    }

    /// <summary>R11: you cannot act on an account that holds a permission you do not hold. Your own account is always fine.</summary>
    public static async Task<Result> EnsureOutranksAsync(UserAccess caller, Guid targetId, IAccessService access, CancellationToken cancellationToken)
    {
        if (caller.UserId == targetId)
        {
            return Result.Success();
        }

        var target = await access.GetAsync(targetId, cancellationToken);
        return target is not null && target.Effective.Any(p => !caller.Effective.Contains(p))
            ? UserErrors.TargetOutranksCaller
            : Result.Success();
    }

    /// <summary>R12: you can only hand out permissions you hold yourself.</summary>
    public static Result EnsureGrantable(UserAccess caller, IEnumerable<string> permissions)
    {
        var missing = permissions.Where(p => !caller.Effective.Contains(p)).Distinct().ToList();
        return missing.Count == 0 ? Result.Success() : RoleErrors.PermissionNotGrantable(missing);
    }

    /// <summary>R13: after <paramref name="change"/> there must still be an active user who can manage roles (if there was one before).</summary>
    public static async Task<Result> EnsureRoleManagerRemainsAsync(
        IApplicationDbContext db, Func<RoleManagerSnapshot, RoleManagerSnapshot> change, CancellationToken cancellationToken)
    {
        var before = await RoleManagerSnapshot.LoadAsync(db, cancellationToken);
        if (!before.HasRoleManager())
        {
            return Result.Success();
        }

        var after = change(await RoleManagerSnapshot.LoadAsync(db, cancellationToken));
        return after.HasRoleManager() ? Result.Success() : UserErrors.LastRoleManager;
    }
}
