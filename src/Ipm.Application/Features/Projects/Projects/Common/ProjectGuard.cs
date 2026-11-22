using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Features.Identity.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Ipm.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Projects.Projects.Common;

/// <summary>
/// The access rules of every project use case. A project the caller cannot see is a 404 (so its existence is not revealed);
/// a project they can see but may not act on is a 403, and the refusal is written to the security events (R21 of part 01).
/// </summary>
internal static class ProjectGuard
{
    /// <summary>Loads the project with its members for reading (no change tracking).</summary>
    public static Task<Result<GuardedProject>> ReadAsync(
        IApplicationDbContext db, IUserContext user, IAccessService access, ISecurityEventLog securityLog, Guid projectId, CancellationToken cancellationToken) =>
        LoadAsync(db, user, access, securityLog, projectId, permission: null, track: false, cancellationToken);

    /// <summary>Loads the project with its members for changing, after checking the caller holds <paramref name="permission"/> in it.</summary>
    public static Task<Result<GuardedProject>> ChangeAsync(
        IApplicationDbContext db, IUserContext user, IAccessService access, ISecurityEventLog securityLog, Guid projectId, string permission, CancellationToken cancellationToken) =>
        LoadAsync(db, user, access, securityLog, projectId, permission, track: true, cancellationToken);

    /// <summary>R8: you can only hand out, change or take away what you hold yourself inside this project.</summary>
    public static Result EnsureGrantableIn(UserAccess caller, Guid projectId, IEnumerable<string> permissions)
    {
        var held = caller.EffectiveIn(projectId);
        var missing = permissions.Where(p => !held.Contains(p)).Distinct().ToList();
        return missing.Count == 0 ? Result.Success() : RoleErrors.PermissionNotGrantable(missing);
    }

    private static async Task<Result<GuardedProject>> LoadAsync(
        IApplicationDbContext db, IUserContext user, IAccessService access, ISecurityEventLog securityLog,
        Guid projectId, string? permission, bool track, CancellationToken cancellationToken)
    {
        var caller = await AccessChecks.CallerAsync(user, access, cancellationToken);
        if (caller.IsFailure)
        {
            return caller.Error;
        }

        var projects = track ? db.Projects.AsQueryable() : db.Projects.AsNoTracking();
        var project = await projects.Include(p => p.Members).SingleOrDefaultAsync(p => p.Id == projectId, cancellationToken);

        // Not there, or not visible to the caller: the same answer, so a hidden project cannot be probed for.
        if (project is null || !caller.Value.Can(Permissions.ProjectsView, projectId))
        {
            return ProjectErrors.NotFound;
        }

        if (permission is not null && !caller.Value.Can(permission, projectId))
        {
            await securityLog.LogAsync(SecurityEventType.PermissionDenied, caller.Value.UserId, null, permission: permission, cancellationToken: cancellationToken);
            return AuthErrors.Forbidden;
        }

        return new GuardedProject(project, caller.Value);
    }
}
