using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Work.WorkItems.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Ipm.Domain.Identity;
using Ipm.Domain.Work;

namespace Ipm.Application.Features.Work.WorkItems.Commands.CloseWorkItem;

internal sealed class CloseWorkItemCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog, TimeProvider time)
    : ICommandHandler<CloseWorkItemCommand, WorkItemResponse>
{
    public async ValueTask<Result<WorkItemResponse>> Handle(CloseWorkItemCommand command, CancellationToken cancellationToken)
    {
        var guarded = await WorkItemGuard.ChangeAsync(db, currentUser, access, securityLog, command.Id, Permissions.WorkflowTransition, cancellationToken);
        if (guarded.IsFailure)
        {
            return guarded.Error;
        }

        var (item, project, caller) = (guarded.Value.Item, guarded.Value.Project, guarded.Value.Caller);
        if (!item.HasVersion(command.ExpectedVersion))
        {
            return Error.StaleVersion;
        }

        var workflow = await WorkItemWorkflows.ForItemAsync(db, project, item, cancellationToken);
        if (workflow is null)
        {
            return WorkItemErrors.WorkflowMissing;
        }

        // R9: the done statuses this item can reach from where it is.
        var reachable = await WorkItemTransitions.ReachableAsync(db, workflow, item.StatusId, cancellationToken);
        var targets = reachable.Where(r => r.Status.Category == StatusCategory.Done).Select(r => r.Status.Id).ToList();

        Guid target;
        if (command.ToStatusId is { } requested)
        {
            if (!targets.Contains(requested))
            {
                return WorkItemErrors.CloseTargetInvalid;
            }

            target = requested;
        }
        else if (targets.Count == 0)
        {
            return WorkItemErrors.CloseTargetInvalid;
        }
        else if (targets.Count > 1)
        {
            return WorkItemErrors.CloseTargetRequired;
        }
        else
        {
            target = targets[0];
        }

        var moved = await WorkItemTransitions.ApplyAsync(db, securityLog, time, item, project, workflow, caller, target, command.Comment, cancellationToken);
        if (moved.IsFailure)
        {
            return moved.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return item.ToResponse();
    }
}
