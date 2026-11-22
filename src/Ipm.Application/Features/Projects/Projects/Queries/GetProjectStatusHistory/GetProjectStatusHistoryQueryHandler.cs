using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Features.Projects.Projects.Common;
using Ipm.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Projects.Projects.Queries.GetProjectStatusHistory;

internal sealed class GetProjectStatusHistoryQueryHandler(IApplicationDbContext db, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog)
    : IQueryHandler<GetProjectStatusHistoryQuery, PagedList<ProjectStatusChangeResponse>>
{
    public async ValueTask<Result<PagedList<ProjectStatusChangeResponse>>> Handle(GetProjectStatusHistoryQuery query, CancellationToken cancellationToken)
    {
        var guarded = await ProjectGuard.ReadAsync(db, currentUser, access, securityLog, query.Id, cancellationToken);
        if (guarded.IsFailure)
        {
            return guarded.Error;
        }

        var changes = db.ProjectStatusChanges.AsNoTracking().Where(c => c.ProjectId == query.Id);
        var total = await changes.CountAsync(cancellationToken);

        var items = await (
                from c in changes
                join u in db.Users on c.ActorId equals u.Id
                orderby c.At descending, c.Id descending
                select new ProjectStatusChangeResponse(c.At, c.FromStatusId, c.ToStatusId, c.ActorId, u.Name, c.Comment, c.ApprovalOverridden))
            .Skip(query.Page.Skip)
            .Take(query.Page.Take)
            .ToListAsync(cancellationToken);

        return new PagedList<ProjectStatusChangeResponse>(items, Math.Max(query.Page.Page, 1), query.Page.Take, total);
    }
}
