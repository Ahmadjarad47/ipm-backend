using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Projects.Projects.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Ipm.Domain.Identity;
using Ipm.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Projects.Projects.Commands.SetProjectWorkflowOverrides;

internal sealed class SetProjectWorkflowOverridesCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog)
    : ICommandHandler<SetProjectWorkflowOverridesCommand, ProjectResponse>
{
    public async ValueTask<Result<ProjectResponse>> Handle(SetProjectWorkflowOverridesCommand command, CancellationToken cancellationToken)
    {
        var guarded = await ProjectGuard.ChangeAsync(
            db, currentUser, access, securityLog, command.Id, Permissions.ProjectWorkItemTypesConfigure, cancellationToken);
        if (guarded.IsFailure)
        {
            return guarded.Error;
        }

        var project = guarded.Value.Project;
        if (!project.HasVersion(command.ExpectedVersion))
        {
            return Error.StaleVersion;
        }

        var editable = project.EnsureEditable(); // R6
        if (editable.IsFailure)
        {
            return editable.Error;
        }

        var typeIds = command.Overrides.Select(o => o.TypeId).Distinct().ToList();
        var workflowIds = command.Overrides.Select(o => o.WorkflowId).Distinct().ToList();
        var types = await db.WorkItemTypes.AsNoTracking().Where(t => typeIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.WorkflowId, cancellationToken);
        var workflows = await db.Workflows.AsNoTracking()
            .Where(w => workflowIds.Contains(w.Id) && w.Scope == StatusScope.WorkItem)
            .Select(w => w.Id)
            .ToListAsync(cancellationToken);

        // R13: both the type and the workflow exist, and the workflow is a work item workflow.
        if (types.Count != typeIds.Count || workflows.Count != workflowIds.Count)
        {
            return ProjectErrors.WorkflowOverrideInvalid;
        }

        // R13: an override equal to the type own workflow is no override at all.
        project.SetWorkflowOverrides(command.Overrides.Where(o => types[o.TypeId] != o.WorkflowId));

        // R17 (part 05): items whose status the new workflow lacks are checked where the items table exists.
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return project.ToResponse();
    }
}
