using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.WorkItemTypes.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Config.WorkItemTypes.Commands.UpdateWorkItemType;

internal sealed class UpdateWorkItemTypeCommandHandler(IApplicationDbContext db, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateWorkItemTypeCommand, WorkItemTypeResponse>
{
    public async ValueTask<Result<WorkItemTypeResponse>> Handle(UpdateWorkItemTypeCommand command, CancellationToken cancellationToken)
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

        var keyUnchanged = type.EnsureKeyUnchanged(command.Key); // R1
        if (keyUnchanged.IsFailure)
        {
            return keyUnchanged.Error;
        }

        // R11 (later parts): disabling a type that a project uses as its default, or that has items, is checked where those tables exist.
        type.Update(command.Name, command.Description, command.Color, command.Icon, command.Order ?? type.SortOrder, command.Active ?? type.IsActive);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return type.ToResponse();
    }
}
