using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.Workflows.Common;
using Ipm.Application.Features.Work.WorkItems.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Config.Workflows.Commands.UpdateWorkflow;

internal sealed class UpdateWorkflowCommandHandler(IApplicationDbContext db, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateWorkflowCommand, WorkflowResponse>
{
    public async ValueTask<Result<WorkflowResponse>> Handle(UpdateWorkflowCommand command, CancellationToken cancellationToken)
    {
        var workflow = await db.Workflows.SingleOrDefaultAsync(w => w.Id == command.Id, cancellationToken);
        if (workflow is null)
        {
            return WorkflowErrors.NotFound;
        }

        if (!workflow.HasVersion(command.ExpectedVersion))
        {
            return Error.StaleVersion;
        }

        var keyUnchanged = workflow.EnsureKeyUnchanged(command.Key); // R5
        if (keyUnchanged.IsFailure)
        {
            return keyUnchanged.Error;
        }

        var scopeUnchanged = workflow.EnsureScopeUnchanged(command.Scope); // R2
        if (scopeUnchanged.IsFailure)
        {
            return scopeUnchanged.Error;
        }

        WorkflowChecks.EnsureDefinitionValid(workflow.Scope, command.StatusIds, command.InitialStatusId, command.Transitions); // R6 to R10

        var statuses = await WorkflowChecks.EnsureStatusesAsync(db, workflow.Scope, command.StatusIds, workflow.StatusIds, cancellationToken);
        if (statuses.IsFailure)
        {
            return statuses.Error; // R6, R12
        }

        // R16 (part 04): a status that projects are in cannot be removed from the project workflow.
        if (workflow.Scope == StatusScope.Project)
        {
            var removed = workflow.StatusIds.Where(id => !command.StatusIds.Contains(id)).ToList();
            var projects = removed.Count == 0 ? 0 : await db.Projects.CountAsync(p => removed.Contains(p.StatusId), cancellationToken);
            if (projects > 0)
            {
                return WorkflowErrors.StatusInUse(projects);
            }
        }

        // R15 (part 03, enforced from part 05): a status that items following this workflow are in cannot be removed from it.
        if (workflow.Scope == StatusScope.WorkItem)
        {
            var removedFromItems = workflow.StatusIds.Where(id => !command.StatusIds.Contains(id)).ToList();
            var stranded = await WorkItemUsage.FollowingInStatusesAsync(db, workflow.Id, removedFromItems, cancellationToken);
            if (stranded > 0)
            {
                return WorkflowErrors.StatusInUseByWorkItems(stranded);
            }
        }

        var defined = workflow.SetDefinition(command.Name, command.StatusIds, command.InitialStatusId, command.Transitions);
        if (defined.IsFailure)
        {
            return defined.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return workflow.ToResponse();
    }
}
