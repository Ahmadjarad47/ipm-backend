using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Work.WorkItems.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Ipm.Domain.Work;

namespace Ipm.Application.Features.Work.WorkItems.Commands.TransitionWorkItem;

internal sealed class TransitionWorkItemCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog, TimeProvider time)
    : ICommandHandler<TransitionWorkItemCommand, WorkItemResponse>
{
    public async ValueTask<Result<WorkItemResponse>> Handle(TransitionWorkItemCommand command, CancellationToken cancellationToken)
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

        var moved = await WorkItemTransitions.ApplyAsync(db, securityLog, time, item, project, workflow, caller, command.ToStatusId, command.Comment, cancellationToken);
        if (moved.IsFailure)
        {
            return moved.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return item.ToResponse();
    }
}
