using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.RelationTypes.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Config.RelationTypes.Commands.UpdateRelationType;

internal sealed class UpdateRelationTypeCommandHandler(IApplicationDbContext db, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateRelationTypeCommand, RelationTypeResponse>
{
    public async ValueTask<Result<RelationTypeResponse>> Handle(UpdateRelationTypeCommand command, CancellationToken cancellationToken)
    {
        var type = await db.RelationTypes.SingleOrDefaultAsync(t => t.Id == command.Id, cancellationToken);
        if (type is null)
        {
            return RelationTypeErrors.NotFound;
        }

        if (!type.HasVersion(command.ExpectedVersion))
        {
            return Error.StaleVersion;
        }

        var keyUnchanged = type.EnsureKeyUnchanged(command.Key); // R6
        if (keyUnchanged.IsFailure)
        {
            return keyUnchanged.Error;
        }

        var pairs = await RelationTypeChecks.EnsurePairTypesExistAsync(db, command.AllowedPairs, cancellationToken);
        if (pairs.IsFailure)
        {
            return pairs.Error; // R8
        }

        // R9 and R10 (part 05): once links exist, direction and the cycle rule are frozen and pairs cannot be narrowed under them.
        var updated = type.Update(command.Name, command.InverseName, command.Directed, command.Acyclic, command.Active ?? type.IsActive, command.AllowedPairs); // R7
        if (updated.IsFailure)
        {
            return updated.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return type.ToResponse();
    }
}
