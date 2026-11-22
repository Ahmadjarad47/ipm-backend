using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Work.WorkItems.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Ipm.Domain.Work;

namespace Ipm.Application.Features.Work.WorkItems.Commands.BulkUpdateWorkItems;

internal sealed class BulkUpdateWorkItemsCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog, TimeProvider time)
    : ICommandHandler<BulkUpdateWorkItemsCommand, IReadOnlyList<BulkItemResult>>
{
    private const int MaxItems = 100;

    public async ValueTask<Result<IReadOnlyList<BulkItemResult>>> Handle(BulkUpdateWorkItemsCommand command, CancellationToken cancellationToken)
    {
        var ids = command.Ids.Distinct().ToList();
        if (ids.Count == 0)
        {
            return WorkItemErrors.BulkEmpty;
        }

        if (ids.Count > MaxItems)
        {
            return WorkItemErrors.BulkTooMany;
        }

        var permission = command.Action.Kind switch
        {
            "assign" => Permissions.TasksAssign,
            "transition" => Permissions.WorkflowTransition,
            _ => Permissions.TasksEdit,
        };

        var results = new List<BulkItemResult>(ids.Count);
        foreach (var id in ids)
        {
            var outcome = await ApplyOneAsync(id, permission, command.Action, cancellationToken);
            results.Add(outcome.Result);
        }

        return results;
    }

    private async Task<(BulkItemResult Result, bool Saved)> ApplyOneAsync(Guid id, string permission, BulkAction action, CancellationToken cancellationToken)
    {
        var guarded = await WorkItemGuard.ChangeAsync(db, currentUser, access, securityLog, id, permission, cancellationToken);
        if (guarded.IsFailure)
        {
            return (new BulkItemResult(id, null, false, guarded.Error.Description), false);
        }

        var (item, project, caller) = (guarded.Value.Item, guarded.Value.Project, guarded.Value.Caller);
        var changed = await ChangeAsync(item, project, caller, action, cancellationToken);
        if (changed.IsFailure)
        {
            return (new BulkItemResult(id, item.Ref, false, changed.Error.Description), false);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (new BulkItemResult(id, item.Ref, true, null), true);
    }

    private async Task<Result> ChangeAsync(
        WorkItem item, Ipm.Domain.Projects.Project project, UserAccess caller, BulkAction action, CancellationToken cancellationToken)
    {
        var editable = item.EnsureEditable();
        if (editable.IsFailure)
        {
            return editable;
        }

        if (project.IsArchived)
        {
            return WorkItemErrors.ProjectArchived;
        }

        switch (action.Kind)
        {
            case "assign":
                var assignees = await WorkItemChecks.EnsureAssigneesAsync(db, project, action.AssigneeIds ?? [], item.AssigneeIds, cancellationToken);
                if (assignees.IsFailure)
                {
                    return assignees;
                }

                item.SetAssignees(action.AssigneeIds ?? []);
                return Result.Success();
            case "priority":
                item.SetPriority(action.Priority ?? item.Priority);
                return Result.Success();
            case "dueDate":
                return item.SetDueDate(action.DueDate);
            case "transition":
                var workflow = await WorkItemWorkflows.ForItemAsync(db, project, item, cancellationToken);
                return workflow is null
                    ? WorkItemErrors.WorkflowMissing
                    : await WorkItemTransitions.ApplyAsync(
                        db, securityLog, time, item, project, workflow, caller, action.ToStatusId ?? Guid.Empty, action.Comment, cancellationToken);
            default:
                return WorkItemErrors.BulkInvalid;
        }
    }
}
