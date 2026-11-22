using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Work.WorkItems.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Ipm.Domain.Work;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Work.Relations.Commands.DeleteRelation;

internal sealed class DeleteRelationCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog)
    : ICommandHandler<DeleteRelationCommand>
{
    public async ValueTask<Result> Handle(DeleteRelationCommand command, CancellationToken cancellationToken)
    {
        var relation = await db.WorkItemRelations.SingleOrDefaultAsync(r => r.Id == command.Id, cancellationToken);
        if (relation is null)
        {
            return RelationErrors.NotFound;
        }

        var source = await WorkItemGuard.ReadAsync(db, currentUser, access, securityLog, relation.SourceId, Permissions.WorkItemRelationsDelete, cancellationToken);
        if (source.IsFailure)
        {
            return source.Error == WorkItemErrors.NotFound ? RelationErrors.NotFound : source.Error;
        }

        if (source.Value.Project.IsArchived)
        {
            return RelationErrors.ProjectArchived; // R7
        }

        db.WorkItemRelations.Remove(relation);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
