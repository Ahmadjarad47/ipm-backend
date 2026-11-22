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

        // R13: a requested workflow must be a work item workflow. Omitted keeps the current one (or binds the default for a row from before part 03).
        var requested = command.WorkflowId ?? type.WorkflowId;
        var workflow = await WorkflowBinding.ResolveAsync(db, requested, cancellationToken);
        if (workflow.IsFailure)
        {
            return workflow.Error;
        }

        // R11 and R15 (later parts): disabling a type that a project uses as its default, or switching its workflow under existing
        // items, is checked where those tables exist.
        type.SetWorkflow(workflow.Value);
        type.Update(command.Name, command.Description, command.Color, command.Icon, command.Order ?? type.SortOrder, command.Active ?? type.IsActive);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return type.ToResponse();
    }
}
