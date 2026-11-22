using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Work.WorkItems.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Ipm.Domain.Identity;
using Ipm.Domain.Work;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Work.WorkItems.Commands.ReopenWorkItem;

internal sealed class ReopenWorkItemCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog, TimeProvider time)
    : ICommandHandler<ReopenWorkItemCommand, WorkItemResponse>
{
    public async ValueTask<Result<WorkItemResponse>> Handle(ReopenWorkItemCommand command, CancellationToken cancellationToken)
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

        if (string.IsNullOrWhiteSpace(command.Comment))
        {
            return WorkItemErrors.ReopenReasonRequired; // R9
        }

        var current = await db.Statuses.AsNoTracking().SingleAsync(s => s.Id == item.StatusId, cancellationToken);
        if (current.Category is not (StatusCategory.Done or StatusCategory.Cancelled))
        {
            return WorkItemErrors.NotClosed; // R9
        }

        var workflow = await WorkItemWorkflows.ForItemAsync(db, project, item, cancellationToken);
        if (workflow is null)
        {
            return WorkItemErrors.WorkflowMissing;
        }

        // R9: the open statuses this item can reach from where it is, in the display order of the statuses.
        var reachable = await WorkItemTransitions.ReachableAsync(db, workflow, item.StatusId, cancellationToken);
        var targets = reachable.Where(r => r.Status.Category is not (StatusCategory.Done or StatusCategory.Cancelled)).Select(r => r.Status.Id).ToList();

        Guid target;
        if (command.ToStatusId is { } requested)
        {
            if (!targets.Contains(requested))
            {
                return WorkItemErrors.ReopenTargetInvalid;
            }

            target = requested;
        }
        else if (targets.Count == 0)
        {
            return WorkItemErrors.ReopenTargetInvalid;
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
