using Ipm.Application.Abstractions.Data;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Work.WorkItems.Common;

/// <summary>
/// R14: how many work items a configuration change would strand (an item in a status its workflow would no longer have, or of a
/// type its project would no longer allow). Archived items count too: they stay readable, so their status must still exist.
/// </summary>
internal static class WorkItemUsage
{
    /// <summary>Items of the project whose type is one of <paramref name="typeIds"/> (R17 of part 04).</summary>
    public static Task<int> OfTypesAsync(IApplicationDbContext db, Guid projectId, IReadOnlyCollection<Guid> typeIds, CancellationToken cancellationToken) =>
        typeIds.Count == 0 ? Task.FromResult(0) : db.WorkItems.CountAsync(i => i.ProjectId == projectId && typeIds.Contains(i.TypeId), cancellationToken);

    /// <summary>Items of the project and type whose status is not one of <paramref name="statusIds"/> (R17 of part 04).</summary>
    public static Task<int> OutsideStatusesAsync(
        IApplicationDbContext db, Guid projectId, Guid typeId, IReadOnlyCollection<Guid> statusIds, CancellationToken cancellationToken) =>
        db.WorkItems.CountAsync(i => i.ProjectId == projectId && i.TypeId == typeId && !statusIds.Contains(i.StatusId), cancellationToken);

    /// <summary>
    /// Items that follow <paramref name="workflowId"/> (through their type, or through a project override) and sit in one of
    /// <paramref name="statusIds"/> (R15 of part 03: removing statuses from a workflow).
    /// </summary>
    public static async Task<int> FollowingInStatusesAsync(
        IApplicationDbContext db, Guid workflowId, IReadOnlyCollection<Guid> statusIds, CancellationToken cancellationToken)
    {
        if (statusIds.Count == 0)
        {
            return 0;
        }

        var total = 0;
        foreach (var (projectId, typeIds) in await FollowersAsync(db, workflowId, cancellationToken))
        {
            total += await db.WorkItems.CountAsync(i => i.ProjectId == projectId && typeIds.Contains(i.TypeId) && statusIds.Contains(i.StatusId), cancellationToken);
        }

        return total;
    }

    /// <summary>
    /// Items of <paramref name="typeId"/> that follow the type own workflow (their project has no override for it) and sit in a status
    /// outside <paramref name="statusIds"/> (R15 of part 03: switching the workflow of a type).
    /// </summary>
    public static async Task<int> OfTypeWithoutOverrideOutsideAsync(
        IApplicationDbContext db, Guid typeId, IReadOnlyCollection<Guid> statusIds, CancellationToken cancellationToken)
    {
        var projects = await db.Projects.AsNoTracking().Select(p => new { p.Id, p.WorkflowOverrides }).ToListAsync(cancellationToken);
        var following = projects.Where(p => p.WorkflowOverrides.TrueForAll(o => o.TypeId != typeId)).Select(p => p.Id).ToList();
        return await db.WorkItems.CountAsync(i => i.TypeId == typeId && following.Contains(i.ProjectId) && !statusIds.Contains(i.StatusId), cancellationToken);
    }

    /// <summary>The (project, types) pairs whose items follow the workflow: types bound to it without a project override, and overrides pointing at it.</summary>
    private static async Task<List<(Guid ProjectId, List<Guid> TypeIds)>> FollowersAsync(IApplicationDbContext db, Guid workflowId, CancellationToken cancellationToken)
    {
        var bound = await db.WorkItemTypes.AsNoTracking().Where(t => t.WorkflowId == workflowId).Select(t => t.Id).ToListAsync(cancellationToken);
        var projects = await db.Projects.AsNoTracking().Select(p => new { p.Id, p.WorkflowOverrides }).ToListAsync(cancellationToken);

        var result = new List<(Guid, List<Guid>)>();
        foreach (var project in projects)
        {
            var typeIds = bound.Where(t => project.WorkflowOverrides.TrueForAll(o => o.TypeId != t))
                .Concat(project.WorkflowOverrides.Where(o => o.WorkflowId == workflowId).Select(o => o.TypeId))
                .Distinct()
                .ToList();
            if (typeIds.Count > 0)
            {
                result.Add((project.Id, typeIds));
            }
        }

        return result;
    }
}
