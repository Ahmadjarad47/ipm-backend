using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.WorkItemTypes.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Config.WorkItemTypes.Commands.CreateWorkItemType;

internal sealed class CreateWorkItemTypeCommandHandler(IApplicationDbContext db, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateWorkItemTypeCommand, WorkItemTypeResponse>
{
    public async ValueTask<Result<WorkItemTypeResponse>> Handle(CreateWorkItemTypeCommand command, CancellationToken cancellationToken)
    {
        var created = WorkItemType.Create(command.Key, command.Name, command.Description, command.Color, command.Icon, command.Order, command.Active); // R1
        if (created.IsFailure)
        {
            return created.Error;
        }

        var key = created.Value.Key;
        if (await db.WorkItemTypes.AnyAsync(t => t.Key == key, cancellationToken))
        {
            return WorkItemTypeErrors.KeyTaken; // R1
        }

        var workflow = await WorkflowBinding.ResolveAsync(db, command.WorkflowId, cancellationToken);
        if (workflow.IsFailure)
        {
            return workflow.Error; // R13, R14
        }

        created.Value.SetWorkflow(workflow.Value);
        db.WorkItemTypes.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return created.Value.ToResponse();
    }
}
