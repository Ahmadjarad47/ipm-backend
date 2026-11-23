using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Approvals.Approvals.Common;
using Ipm.Application.Features.Work.WorkItems.Common;
using Ipm.Domain.Approvals;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Ipm.Domain.Work;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Work.WorkItems.Commands.UpdateWorkItem;

internal sealed class UpdateWorkItemCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog)
    : ICommandHandler<UpdateWorkItemCommand, WorkItemResponse>
{
    public async ValueTask<Result<WorkItemResponse>> Handle(UpdateWorkItemCommand command, CancellationToken cancellationToken)
    {
        var guarded = await WorkItemGuard.ChangeAsync(db, currentUser, access, securityLog, command.Id, Permissions.TasksView, cancellationToken);
        if (guarded.IsFailure)
        {
            return guarded.Error;
        }

        var (item, project, caller) = (guarded.Value.Item, guarded.Value.Project, guarded.Value.Caller);
        if (!item.HasVersion(command.ExpectedVersion))
        {
            return Error.StaleVersion;
        }

        // R2: the status, the type and the parent have operations of their own.
        if (command.StatusId is { } statusId && statusId != item.StatusId)
        {
            return WorkItemErrors.StatusChangeNotAllowed;
        }

        if (command.TypeId is { } typeId && typeId != item.TypeId)
        {
            return WorkItemErrors.TypeChangeNotAllowed;
        }

        if (command.ParentId is { } parentId && parentId != item.ParentId)
        {
            return WorkItemErrors.ParentChangeNotAllowed;
        }

        var editable = item.EnsureEditable();
        if (editable.IsFailure)
        {
            return editable.Error;
        }

        if (project.IsArchived)
        {
            return WorkItemErrors.ProjectArchived;
        }

        var type = await db.WorkItemTypes.AsNoTracking().SingleAsync(t => t.Id == item.TypeId, cancellationToken);
        var fields = await WorkItemChecks.EnsureFieldsAsync(db, type, command.Fields, item.Fields, cancellationToken); // R5

        // R2: the assignees need tasks.assign, everything else tasks.edit.
        var assigneesChanged = !command.AssigneeIds.Order().SequenceEqual(item.AssigneeIds.Order());
        var othersChanged = command.Title.Trim() != item.Title
            || command.Description.Trim() != item.Description
            || command.Priority != item.Priority
            || command.StartDate != item.StartDate
            || command.DueDate != item.DueDate
            || !WorkItemFieldValues.Same(fields, item.Fields);

        if (othersChanged || !assigneesChanged)
        {
            var edit = await WorkItemGuard.RequireAsync(caller, project.Id, Permissions.TasksEdit, securityLog, cancellationToken);
            if (edit.IsFailure)
            {
                return edit.Error;
            }
        }

        if (assigneesChanged)
        {
            var assign = await WorkItemGuard.RequireAsync(caller, project.Id, Permissions.TasksAssign, securityLog, cancellationToken);
            if (assign.IsFailure)
            {
                return assign.Error;
            }

            var assignees = await WorkItemChecks.EnsureAssigneesAsync(db, project, command.AssigneeIds, item.AssigneeIds, cancellationToken);
            if (assignees.IsFailure)
            {
                return assignees.Error; // R4
            }
        }

        var changedFields = new List<string>();
        if (command.Title.Trim() != item.Title) { changedFields.Add("title"); }
        if (command.Description.Trim() != item.Description) { changedFields.Add("description"); }
        if (command.Priority != item.Priority) { changedFields.Add("priority"); }
        if (command.StartDate != item.StartDate) { changedFields.Add("startDate"); }
        if (command.DueDate != item.DueDate) { changedFields.Add("dueDate"); }
        if (assigneesChanged) { changedFields.Add("assigneeIds"); }

        var direct = await SensitiveChangeGuard.EnsureAsync(db, caller, ApprovalSubjectType.WorkItem, item.Id, changedFields, cancellationToken); // 06b R8
        if (direct.IsFailure)
        {
            return direct.Error;
        }

        var updated = item.Update(command.Title, command.Description, command.Priority, command.StartDate, command.DueDate, command.AssigneeIds, fields);
        if (updated.IsFailure)
        {
            return updated.Error;
        }

        await ApprovalSupersession.SupersedeAsync(db, ApprovalSubjectType.WorkItem, item.Id, changedFields, cancellationToken); // R11 of part 06
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return item.ToResponse();
    }
}
