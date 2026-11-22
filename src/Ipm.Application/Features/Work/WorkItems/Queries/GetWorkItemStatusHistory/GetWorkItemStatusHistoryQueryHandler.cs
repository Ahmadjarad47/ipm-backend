using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Features.Work.WorkItems.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Work.WorkItems.Queries.GetWorkItemStatusHistory;

internal sealed class GetWorkItemStatusHistoryQueryHandler(IApplicationDbContext db, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog)
    : IQueryHandler<GetWorkItemStatusHistoryQuery, PagedList<WorkItemStatusChangeResponse>>
{
    public async ValueTask<Result<PagedList<WorkItemStatusChangeResponse>>> Handle(GetWorkItemStatusHistoryQuery query, CancellationToken cancellationToken)
    {
        var guarded = await WorkItemGuard.ReadAsync(db, currentUser, access, securityLog, query.Id, Permissions.TasksView, cancellationToken);
        if (guarded.IsFailure)
        {
            return guarded.Error;
        }

        var changes = db.WorkItemStatusChanges.AsNoTracking().Where(c => c.WorkItemId == query.Id);
        var total = await changes.CountAsync(cancellationToken);

        var items = await (
                from c in changes
                join u in db.Users on c.ActorId equals u.Id
                orderby c.At descending, c.Id descending
                select new WorkItemStatusChangeResponse(c.At, c.FromStatusId, c.ToStatusId, c.ActorId, u.Name, c.Comment, c.ApprovalOverridden))
            .Skip(query.Page.Skip)
            .Take(query.Page.Take)
            .ToListAsync(cancellationToken);

        return new PagedList<WorkItemStatusChangeResponse>(items, Math.Max(query.Page.Page, 1), query.Page.Take, total);
    }
}
