using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Work.WorkItems.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Ipm.Domain.Work;

namespace Ipm.Application.Features.Work.WorkItems.Commands.SetWorkItemParent;

internal sealed class SetWorkItemParentCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog)
    : ICommandHandler<SetWorkItemParentCommand, WorkItemResponse>
{
    public async ValueTask<Result<WorkItemResponse>> Handle(SetWorkItemParentCommand command, CancellationToken cancellationToken)
    {
        var guarded = await WorkItemGuard.ChangeAsync(db, currentUser, access, securityLog, command.Id, Permissions.TasksEdit, cancellationToken);
        if (guarded.IsFailure)
        {
            return guarded.Error;
        }

        var (item, project) = (guarded.Value.Item, guarded.Value.Project);
        if (!item.HasVersion(command.ExpectedVersion))
        {
            return Error.StaleVersion;
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

        if (command.ParentId is { } parentId)
        {
            var parent = await WorkItemParents.EnsureAsync(db, project.Id, item.TypeId, item.Id, parentId, cancellationToken);
            if (parent.IsFailure)
            {
                return parent.Error; // R6
            }
        }

        item.SetParent(command.ParentId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return item.ToResponse();
    }
}
