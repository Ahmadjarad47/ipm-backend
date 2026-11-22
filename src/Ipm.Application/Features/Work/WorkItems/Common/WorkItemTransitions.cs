using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Features.Identity.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Ipm.Domain.Identity;
using Ipm.Domain.Projects;
using Ipm.Domain.Work;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Work.WorkItems.Common;

/// <summary>
/// R7 to R9: the rules of moving an item to another status, shared by transition, close, reopen and the bulk transition.
/// The item is changed and its status change added to the context; the caller saves.
/// </summary>
internal static class WorkItemTransitions
{
    /// <summary>The statuses an item can reach from where it is, with the transition that gets there.</summary>
    public static async Task<IReadOnlyList<(Status Status, WorkflowTransition Transition)>> ReachableAsync(
        IApplicationDbContext db, Workflow workflow, Guid fromStatusId, CancellationToken cancellationToken)
    {
        var moves = workflow.Transitions.Where(t => t.FromStatusId == fromStatusId).ToList();
        var ids = moves.Select(m => m.ToStatusId).ToList();
        var statuses = await db.Statuses.AsNoTracking().Where(s => ids.Contains(s.Id) && s.IsActive).ToDictionaryAsync(s => s.Id, cancellationToken);
        return [.. moves.Where(m => statuses.ContainsKey(m.ToStatusId)).Select(m => (statuses[m.ToStatusId], m)).OrderBy(x => x.Item1.SortOrder)];
    }

    public static async Task<Result> ApplyAsync(
        IApplicationDbContext db, ISecurityEventLog securityLog, TimeProvider time,
        WorkItem item, Project project, Workflow workflow, UserAccess caller, Guid toStatusId, string? comment, CancellationToken cancellationToken)
    {
        if (item.IsArchived)
        {
            return WorkItemErrors.Archived;
        }

        if (project.IsArchived)
        {
            return WorkItemErrors.ProjectArchived;
        }

        var statuses = await db.Statuses.AsNoTracking()
            .Where(s => s.Id == item.StatusId || s.Id == toStatusId)
            .ToDictionaryAsync(s => s.Id, cancellationToken);
        if (!statuses.TryGetValue(item.StatusId, out var from) || !statuses.TryGetValue(toStatusId, out var to))
        {
            return WorkItemErrors.TransitionNotAllowed(statuses.GetValueOrDefault(item.StatusId)?.Name ?? "—", "—");
        }

        var transition = workflow.FindTransition(from.Id, to.Id);
        if (transition is null)
        {
            return WorkItemErrors.TransitionNotAllowed(from.Name, to.Name); // R7
        }

        if (!to.IsActive)
        {
            return WorkItemErrors.TargetStatusInactive; // R7
        }

        if (transition.Permission is { } permission && !caller.Can(permission, project.Id))
        {
            await securityLog.LogAsync(SecurityEventType.PermissionDenied, caller.UserId, null, permission: permission, cancellationToken: cancellationToken);
            return AuthErrors.Forbidden; // R7
        }

        if (transition.RequiresAssignee && item.AssigneeIds.Count == 0)
        {
            return WorkItemErrors.AssigneeRequired; // R7
        }

        // R7 blockedByOpenDependencies: the relations between items arrive with part 05b, so nothing blocks an item until then.

        // R8: until the approvals engine exists (part 06), only a holder of audit.override may make a move that needs an approval.
        var overridden = false;
        if (transition.RequiresApproval)
        {
            if (!caller.Has(Permissions.AuditOverride))
            {
                return WorkItemErrors.ApprovalRequired;
            }

            overridden = true;
        }

        item.MoveTo(to.Id);
        db.WorkItemStatusChanges.Add(WorkItemStatusChange.Create(item.Id, from.Id, to.Id, caller.UserId, comment, time.GetUtcNow(), overridden));
        return Result.Success();
    }
}
