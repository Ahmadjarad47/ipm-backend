using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.Statuses.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Config.Statuses.Commands.CreateStatus;

internal sealed class CreateStatusCommandHandler(IApplicationDbContext db, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateStatusCommand, StatusResponse>
{
    public async ValueTask<Result<StatusResponse>> Handle(CreateStatusCommand command, CancellationToken cancellationToken)
    {
        var created = Status.Create(
            command.Key, command.Name, command.Description, command.Color, command.Category, command.Scope, command.Order, command.Active); // R1
        if (created.IsFailure)
        {
            return created.Error;
        }

        var key = created.Value.Key;
        var scope = created.Value.Scope;
        if (await db.Statuses.AnyAsync(s => s.Scope == scope && s.Key == key, cancellationToken))
        {
            return StatusErrors.KeyTaken; // R1: unique inside the scope
        }

        db.Statuses.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return created.Value.ToResponse();
    }
}
