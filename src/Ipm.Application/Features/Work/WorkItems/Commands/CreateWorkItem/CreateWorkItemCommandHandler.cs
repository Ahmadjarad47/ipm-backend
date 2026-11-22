using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Projects.Projects.Common;
using Ipm.Application.Features.Work.WorkItems.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Ipm.Domain.Projects;
using Ipm.Domain.Work;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Work.WorkItems.Commands.CreateWorkItem;

internal sealed class CreateWorkItemCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, IWorkItemNumbering numbering, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog)
    : ICommandHandler<CreateWorkItemCommand, WorkItemResponse>
{
    public async ValueTask<Result<WorkItemResponse>> Handle(CreateWorkItemCommand command, CancellationToken cancellationToken)
    {
        var guarded = await ProjectGuard.ChangeAsync(db, currentUser, access, securityLog, command.ProjectId, Permissions.TasksCreate, cancellationToken);
        if (guarded.IsFailure)
        {
            return guarded.Error;
        }

        var (project, caller) = (guarded.Value.Project, guarded.Value.Caller);
        if (project.IsArchived)
        {
            return WorkItemErrors.ProjectArchived; // R1
        }

        var type = await db.WorkItemTypes.AsNoTracking().SingleOrDefaultAsync(t => t.Id == command.TypeId, cancellationToken);
        if (type is not { IsActive: true } || !project.AllowedTypeIds.Contains(type.Id))
        {
            return WorkItemErrors.TypeNotAllowed; // R1
        }

        var workflow = await WorkItemWorkflows.ForAsync(db, project, type, cancellationToken);
        if (workflow is null)
        {
            return WorkItemErrors.WorkflowMissing; // R1: the status is the initial one of the workflow, never one the caller picked
        }

        if (command.AssigneeIds.Count > 0)
        {
            var assign = await WorkItemGuard.RequireAsync(caller, project.Id, Permissions.TasksAssign, securityLog, cancellationToken);
            if (assign.IsFailure)
            {
                return assign.Error;
            }

            var assignees = await WorkItemChecks.EnsureAssigneesAsync(db, project, command.AssigneeIds, [], cancellationToken);
            if (assignees.IsFailure)
            {
                return assignees.Error; // R4
            }
        }

        if (command.ParentId is { } parentId)
        {
            var parent = await WorkItemParents.EnsureAsync(db, project.Id, type.Id, null, parentId, cancellationToken);
            if (parent.IsFailure)
            {
                return parent.Error; // R6
            }
        }

        var fields = await WorkItemChecks.EnsureFieldsAsync(db, type, command.Fields, null, cancellationToken); // R5

        var number = await numbering.NextAsync(project.Id, cancellationToken);
        var created = WorkItem.Create(
            project.Id, project.Code, number, type.Id, workflow.InitialStatusId, command.Title, command.Description, command.Priority,
            command.StartDate, command.DueDate, command.ParentId, command.AssigneeIds, fields);
        if (created.IsFailure)
        {
            return created.Error;
        }

        db.WorkItems.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return created.Value.ToResponse();
    }
}
