using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.RelationTypes.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Config.RelationTypes.Commands.CreateRelationType;

internal sealed class CreateRelationTypeCommandHandler(IApplicationDbContext db, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateRelationTypeCommand, RelationTypeResponse>
{
    public async ValueTask<Result<RelationTypeResponse>> Handle(CreateRelationTypeCommand command, CancellationToken cancellationToken)
    {
        var created = RelationType.Create(
            command.Key, command.Name, command.InverseName, command.Directed, command.Acyclic, command.Active, command.AllowedPairs); // R6, R7
        if (created.IsFailure)
        {
            return created.Error;
        }

        var key = created.Value.Key;
        if (await db.RelationTypes.AnyAsync(t => t.Key == key, cancellationToken))
        {
            return RelationTypeErrors.KeyTaken; // R6
        }

        var pairs = await RelationTypeChecks.EnsurePairTypesExistAsync(db, created.Value.AllowedPairs, cancellationToken);
        if (pairs.IsFailure)
        {
            return pairs.Error; // R8
        }

        db.RelationTypes.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return created.Value.ToResponse();
    }
}
