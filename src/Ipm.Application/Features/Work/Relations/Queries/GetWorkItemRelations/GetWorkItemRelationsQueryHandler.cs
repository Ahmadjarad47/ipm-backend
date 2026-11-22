using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Work.Relations.Common;
using Ipm.Application.Features.Work.WorkItems.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Work.Relations.Queries.GetWorkItemRelations;

internal sealed class GetWorkItemRelationsQueryHandler(IApplicationDbContext db, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog)
    : IQueryHandler<GetWorkItemRelationsQuery, IReadOnlyList<RelationViewResponse>>
{
    public async ValueTask<Result<IReadOnlyList<RelationViewResponse>>> Handle(GetWorkItemRelationsQuery query, CancellationToken cancellationToken)
    {
        var guarded = await WorkItemGuard.ReadAsync(db, currentUser, access, securityLog, query.Id, Permissions.WorkItemRelationsView, cancellationToken);
        if (guarded.IsFailure)
        {
            return guarded.Error;
        }

        var (item, caller) = (guarded.Value.Item, guarded.Value.Caller);
        var relations = await db.WorkItemRelations.AsNoTracking()
            .Where(r => r.SourceId == item.Id || r.TargetId == item.Id)
            .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
            .ToListAsync(cancellationToken);

        var otherIds = relations.Select(r => r.SourceId == item.Id ? r.TargetId : r.SourceId).Distinct().ToList();
        var others = await db.WorkItems.AsNoTracking().Where(i => otherIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, cancellationToken);
        var typeIds = relations.Select(r => r.RelationTypeId).Distinct().ToList();
        var types = await db.RelationTypes.AsNoTracking().Where(t => typeIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, cancellationToken);

        var views = new List<RelationViewResponse>();
        foreach (var relation in relations)
        {
            var outgoing = relation.SourceId == item.Id;
            if (!others.TryGetValue(outgoing ? relation.TargetId : relation.SourceId, out var other) || !caller.Can(Permissions.ProjectsView, other.ProjectId))
            {
                continue; // R8: a link to an item the caller cannot see is not shown
            }

            var type = types[relation.RelationTypeId];
            views.Add(new RelationViewResponse(relation.ToResponse(), outgoing ? type.Name : type.InverseName, other.ToResponse(), outgoing));
        }

        return views;
    }
}
