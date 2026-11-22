using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.Statuses.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Config.Statuses.Commands.UpdateStatus;

internal sealed class UpdateStatusCommandHandler(IApplicationDbContext db, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateStatusCommand, StatusResponse>
{
    public async ValueTask<Result<StatusResponse>> Handle(UpdateStatusCommand command, CancellationToken cancellationToken)
    {
        var status = await db.Statuses.SingleOrDefaultAsync(s => s.Id == command.Id, cancellationToken);
        if (status is null)
        {
            return StatusErrors.NotFound;
        }

        if (!status.HasVersion(command.ExpectedVersion))
        {
            return Error.StaleVersion;
        }

        var keyUnchanged = status.EnsureKeyUnchanged(command.Key); // R1
        if (keyUnchanged.IsFailure)
        {
            return keyUnchanged.Error;
        }

        var scopeUnchanged = status.EnsureScopeUnchanged(command.Scope); // R2
        if (scopeUnchanged.IsFailure)
        {
            return scopeUnchanged.Error;
        }

        var active = command.Active ?? status.IsActive;
        if (status.IsActive && !active)
        {
            // R11: a workflow cannot start from a status that is switched off.
            var initialIn = await db.Workflows.AsNoTracking()
                .Where(w => w.InitialStatusId == status.Id)
                .Select(w => w.Name)
                .ToListAsync(cancellationToken);
            if (initialIn.Count > 0)
            {
                return StatusErrors.IsInitialStatus(initialIn);
            }

            // R15 (parts 04 and 05): a status that projects or work items are in is checked where those tables exist.
        }

        status.Update(command.Name, command.Description, command.Color, command.Category, command.Order ?? status.SortOrder, active);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return status.ToResponse();
    }
}
