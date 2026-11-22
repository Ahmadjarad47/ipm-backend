using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Features.Identity.Common;
using Ipm.Application.Features.Projects.Projects.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Ipm.Domain.Projects;
using Ipm.Domain.Work;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Work.WorkItems.Common;

/// <summary>
/// The access rules of every work item use case, built on <see cref="ProjectGuard"/>. An item of a project the caller cannot see is a
/// 404 (so its existence is not revealed); an item they can see but may not act on is a 403 and the refusal is logged.
/// </summary>
internal static class WorkItemGuard
{
    /// <summary>The item (no change tracking) after checking the caller holds <paramref name="permission"/> in its project.</summary>
    public static Task<Result<GuardedWorkItem>> ReadAsync(
        IApplicationDbContext db, IUserContext user, IAccessService access, ISecurityEventLog securityLog, Guid itemId, string permission, CancellationToken cancellationToken) =>
        LoadAsync(db, user, access, securityLog, itemId, permission, track: false, cancellationToken);

    /// <summary>The item (tracked) for changing, after checking the caller holds <paramref name="permission"/> in its project.</summary>
    public static Task<Result<GuardedWorkItem>> ChangeAsync(
        IApplicationDbContext db, IUserContext user, IAccessService access, ISecurityEventLog securityLog, Guid itemId, string permission, CancellationToken cancellationToken) =>
        LoadAsync(db, user, access, securityLog, itemId, permission, track: true, cancellationToken);

    /// <summary>A further permission the same request needs inside the project; the refusal is written to the security events.</summary>
    public static async Task<Result> RequireAsync(
        UserAccess caller, Guid projectId, string permission, ISecurityEventLog securityLog, CancellationToken cancellationToken)
    {
        if (caller.Can(permission, projectId))
        {
            return Result.Success();
        }

        await securityLog.LogAsync(SecurityEventType.PermissionDenied, caller.UserId, null, permission: permission, cancellationToken: cancellationToken);
        return AuthErrors.Forbidden;
    }

    private static async Task<Result<GuardedWorkItem>> LoadAsync(
        IApplicationDbContext db, IUserContext user, IAccessService access, ISecurityEventLog securityLog,
        Guid itemId, string permission, bool track, CancellationToken cancellationToken)
    {
        var items = track ? db.WorkItems.AsQueryable() : db.WorkItems.AsNoTracking();
        var item = await items.SingleOrDefaultAsync(i => i.Id == itemId, cancellationToken);
        if (item is null)
        {
            return WorkItemErrors.NotFound;
        }

        var project = track
            ? await ProjectGuard.ChangeAsync(db, user, access, securityLog, item.ProjectId, permission, cancellationToken)
            : await ProjectGuard.ReadWithAsync(db, user, access, securityLog, item.ProjectId, permission, cancellationToken);
        if (project.IsFailure)
        {
            return project.Error == ProjectErrors.NotFound ? WorkItemErrors.NotFound : project.Error;
        }

        return new GuardedWorkItem(item, project.Value.Project, project.Value.Caller);
    }
}
