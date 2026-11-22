using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.WorkItemTypes.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Config.WorkItemTypes.Commands.SetWorkItemTypeChildren;

internal sealed class SetWorkItemTypeChildrenCommandHandler(IApplicationDbContext db, IUnitOfWork unitOfWork)
    : ICommandHandler<SetWorkItemTypeChildrenCommand, WorkItemTypeResponse>
{
    public async ValueTask<Result<WorkItemTypeResponse>> Handle(SetWorkItemTypeChildrenCommand command, CancellationToken cancellationToken)
    {
        var type = await db.WorkItemTypes.SingleOrDefaultAsync(t => t.Id == command.Id, cancellationToken);
        if (type is null)
        {
            return WorkItemTypeErrors.NotFound;
        }

        if (!type.HasVersion(command.ExpectedVersion))
        {
            return Error.StaleVersion;
        }

        var wanted = command.ChildTypeIds.Distinct().ToList();
        var existing = await db.WorkItemTypes.CountAsync(t => wanted.Contains(t.Id), cancellationToken);
        if (existing != wanted.Count)
        {
            return WorkItemTypeErrors.ChildTypeNotFound; // R5
        }

        type.SetAllowedChildren(wanted);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return type.ToResponse();
    }
}
