using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Projects.Projects.Common;
using Ipm.Application.Features.Work.Relations.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Work.Relations.Queries.GetProjectRelations;

internal sealed class GetProjectRelationsQueryHandler(IApplicationDbContext db, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog)
    : IQueryHandler<GetProjectRelationsQuery, IReadOnlyList<RelationResponse>>
{
    public async ValueTask<Result<IReadOnlyList<RelationResponse>>> Handle(GetProjectRelationsQuery query, CancellationToken cancellationToken)
    {
        var guarded = await ProjectGuard.ReadWithAsync(db, currentUser, access, securityLog, query.ProjectId, Permissions.WorkItemRelationsView, cancellationToken);
        if (guarded.IsFailure)
        {
            return guarded.Error;
        }

        var relations = await (
                from r in db.WorkItemRelations.AsNoTracking()
                join s in db.WorkItems on r.SourceId equals s.Id
                join t in db.WorkItems on r.TargetId equals t.Id
                where s.ProjectId == query.ProjectId && t.ProjectId == query.ProjectId && !s.IsArchived && !t.IsArchived
                orderby r.CreatedAt, r.Id
                select r)
            .ToListAsync(cancellationToken);
        return relations.Select(r => r.ToResponse()).ToList();
    }
}
