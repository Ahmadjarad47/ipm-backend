using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.WorkItemTypes.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Config.WorkItemTypes.Commands.SetWorkItemTypeFields;

internal sealed class SetWorkItemTypeFieldsCommandHandler(IApplicationDbContext db, IUnitOfWork unitOfWork)
    : ICommandHandler<SetWorkItemTypeFieldsCommand, WorkItemTypeResponse>
{
    public async ValueTask<Result<WorkItemTypeResponse>> Handle(SetWorkItemTypeFieldsCommand command, CancellationToken cancellationToken)
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

        var changed = type.SetFields(command.Fields); // R3
        if (changed.IsFailure)
        {
            return changed.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return type.ToResponse();
    }
}
