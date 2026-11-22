using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.Workflows.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Config.Workflows.Commands.CreateWorkflow;

internal sealed class CreateWorkflowCommandHandler(IApplicationDbContext db, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateWorkflowCommand, WorkflowResponse>
{
    public async ValueTask<Result<WorkflowResponse>> Handle(CreateWorkflowCommand command, CancellationToken cancellationToken)
    {
        WorkflowChecks.EnsureDefinitionValid(command.Scope, command.StatusIds, command.InitialStatusId, command.Transitions); // R6 to R10

        var created = Workflow.Create(
            command.Key, command.Name, command.Scope, command.StatusIds, command.InitialStatusId, command.Transitions); // R5
        if (created.IsFailure)
        {
            return created.Error;
        }

        var key = created.Value.Key;
        if (await db.Workflows.AnyAsync(w => w.Key == key, cancellationToken))
        {
            return WorkflowErrors.KeyTaken; // R5
        }

        var statuses = await WorkflowChecks.EnsureStatusesAsync(db, command.Scope, command.StatusIds, [], cancellationToken);
        if (statuses.IsFailure)
        {
            return statuses.Error; // R6, R12
        }

        db.Workflows.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return created.Value.ToResponse();
    }
}
