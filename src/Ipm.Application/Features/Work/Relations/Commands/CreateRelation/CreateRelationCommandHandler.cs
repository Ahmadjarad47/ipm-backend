using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Common;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Approvals.Approvals.Common;
using Ipm.Application.Features.Work.Relations.Common;
using Ipm.Application.Features.Work.WorkItems.Common;
using Ipm.Domain.Approvals;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Ipm.Domain.Work;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Work.Relations.Commands.CreateRelation;

internal sealed class CreateRelationCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog)
    : ICommandHandler<CreateRelationCommand, RelationResponse>
{
    public async ValueTask<Result<RelationResponse>> Handle(CreateRelationCommand command, CancellationToken cancellationToken)
    {
        var sourceGuard = await WorkItemGuard.ReadAsync(db, currentUser, access, securityLog, command.SourceId, Permissions.WorkItemRelationsCreate, cancellationToken);
        if (sourceGuard.IsFailure)
        {
            return sourceGuard.Error;
        }

        // The target only has to be visible: a link into another project is refused below (R2).
        var targetGuard = await WorkItemGuard.ReadAsync(db, currentUser, access, securityLog, command.TargetId, Permissions.ProjectsView, cancellationToken);
        if (targetGuard.IsFailure)
        {
            return targetGuard.Error;
        }

        var (source, sourceProject) = (sourceGuard.Value.Item, sourceGuard.Value.Project);
        var (target, targetProject) = (targetGuard.Value.Item, targetGuard.Value.Project);

        var type = await db.RelationTypes.AsNoTracking().SingleOrDefaultAsync(t => t.Id == command.RelationTypeId, cancellationToken);
        if (type is null)
        {
            return RelationErrors.TypeNotFound;
        }

        if (!type.IsActive)
        {
            return RelationErrors.TypeInactive; // R1, R12
        }

        if (source.Id == target.Id)
        {
            return RelationErrors.SelfLink; // R1
        }

        if (source.IsArchived || target.IsArchived)
        {
            return RelationErrors.ItemArchived; // R1
        }

        if (source.ProjectId != target.ProjectId && !(await db.CurrentSettingsAsync(cancellationToken)).AllowCrossProjectRelations)
        {
            return RelationErrors.CrossProjectNotAllowed; // R2: unless the organization allows links between projects (spec 09 R5-c)
        }

        if (sourceProject.IsArchived || targetProject.IsArchived)
        {
            return RelationErrors.ProjectArchived; // R2
        }

        if ((command.SourceVersion is { } sv && !source.HasVersion(sv)) || (command.TargetVersion is { } tv && !target.HasVersion(tv)))
        {
            return Error.StaleVersion; // R6
        }

        if (!type.AllowsPair(source.TypeId, target.TypeId))
        {
            var names = await db.WorkItemTypes.AsNoTracking()
                .Where(t => t.Id == source.TypeId || t.Id == target.TypeId)
                .ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);
            return RelationErrors.PairNotAllowed(type.Name, names.GetValueOrDefault(source.TypeId, "—"), names.GetValueOrDefault(target.TypeId, "—")); // R3
        }

        var duplicate = await db.WorkItemRelations.AnyAsync(
            r => r.RelationTypeId == type.Id
                && ((r.SourceId == source.Id && r.TargetId == target.Id) || (r.SourceId == target.Id && r.TargetId == source.Id)),
            cancellationToken);
        if (duplicate)
        {
            return RelationErrors.Duplicate(type.IsDirected); // R4
        }

        if (type.IsAcyclic && await RelationCycles.WouldCloseAsync(db, type.Id, source.Id, target.Id, cancellationToken))
        {
            return RelationErrors.Cycle(type.Name); // R5
        }

        var created = WorkItemRelation.Create(source.Id, target.Id, type.Id);
        if (created.IsFailure)
        {
            return created.Error;
        }

        db.WorkItemRelations.Add(created.Value);
        await ApprovalSupersession.SupersedeAsync(db, ApprovalSubjectType.WorkItem, source.Id, ["relations"], cancellationToken); // R11 of part 06
        await ApprovalSupersession.SupersedeAsync(db, ApprovalSubjectType.WorkItem, target.Id, ["relations"], cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return created.Value.ToResponse();
    }
}
