using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Work.WorkItems.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Ipm.Domain.Work;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Work.WorkItems.Commands.ArchiveWorkItem;

internal sealed class ArchiveWorkItemCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog)
    : ICommandHandler<ArchiveWorkItemCommand>
{
    public async ValueTask<Result> Handle(ArchiveWorkItemCommand command, CancellationToken cancellationToken)
    {
        var guarded = await WorkItemGuard.ChangeAsync(db, currentUser, access, securityLog, command.Id, Permissions.TasksDelete, cancellationToken);
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

        // R3: the user moves the children first; nothing is archived silently under an item.
        if (await db.WorkItems.AnyAsync(i => i.ParentId == item.Id && !i.IsArchived, cancellationToken))
        {
            return WorkItemErrors.HasActiveChildren;
        }

        item.Archive();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
