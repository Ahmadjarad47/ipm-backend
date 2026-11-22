using Ipm.Application.Abstractions.Data;
using Ipm.Domain.Approvals;
using Ipm.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Approvals.Approvals.Common;

/// <summary>R5: who the approvers of a stage are for one request. Resolved once at submission and then frozen.</summary>
internal static class ApproverResolver
{
    private const string ProjectManagerRoleKey = "project_manager";

    /// <summary>The direct approvers of one stage: a user, the holders of a role, or the holder of a position.</summary>
    public static async Task<List<Guid>> ResolveStageAsync(
        IApplicationDbContext db, ApprovalStage stage, Project project, Guid requesterId, CancellationToken cancellationToken)
    {
        var ids = new List<Guid>();
        switch (stage.ApproverKind)
        {
            case ApproverKind.User:
                ids.Add(Guid.Parse(stage.ApproverId));
                break;
            case ApproverKind.Role:
                var roleId = Guid.Parse(stage.ApproverId);
                ids.AddRange(project.Members.Where(m => m.RoleId == roleId).Select(m => m.UserId));
                ids.AddRange(await db.Users.AsNoTracking().Where(u => u.RoleIds.Contains(roleId)).Select(u => u.Id).ToListAsync(cancellationToken));
                break;
            default:
                var parsed = ApprovalTemplateDefinition.TryParsePosition(stage.ApproverId, out var position);
                if (parsed && position == ApproverPosition.ProjectOwner)
                {
                    ids.Add(project.OwnerId);
                }
                else if (parsed && position == ApproverPosition.DirectManager)
                {
                    var manager = await db.Users.AsNoTracking().Where(u => u.Id == requesterId).Select(u => u.ManagerId).SingleOrDefaultAsync(cancellationToken);
                    if (manager is { } managerId)
                    {
                        ids.Add(managerId);
                    }
                }
                else
                {
                    var managerRoles = await db.Roles.AsNoTracking().Where(r => r.Key == ProjectManagerRoleKey).Select(r => r.Id).ToListAsync(cancellationToken);
                    ids.AddRange(project.Members.Where(m => managerRoles.Contains(m.RoleId)).Select(m => m.UserId));
                }

                break;
        }

        // Only active users can decide.
        var distinct = ids.Distinct().ToList();
        return await db.Users.AsNoTracking().Where(u => distinct.Contains(u.Id) && u.IsActive).Select(u => u.Id).ToListAsync(cancellationToken);
    }

    /// <summary>Resolves every stage once (R5). Returns the frozen approvers, or the first stage that has none (R4).</summary>
    public static async Task<(List<ResolvedApprovers> Resolved, ApprovalStage? Unresolved)> FreezeAsync(
        IApplicationDbContext db, IEnumerable<ApprovalStage> stages, Project project, Guid requesterId, CancellationToken cancellationToken)
    {
        var resolved = new List<ResolvedApprovers>();
        foreach (var stage in stages.OrderBy(s => s.Order))
        {
            var users = await ResolveStageAsync(db, stage, project, requesterId, cancellationToken);
            if (users.Count == 0)
            {
                return (resolved, stage);
            }

            resolved.Add(new ResolvedApprovers(stage.Order, users));
        }

        return (resolved, null);
    }
}
