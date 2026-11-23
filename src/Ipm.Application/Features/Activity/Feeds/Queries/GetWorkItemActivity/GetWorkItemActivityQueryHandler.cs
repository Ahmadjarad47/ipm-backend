using Ipm.Application.Abstractions.Auditing;
using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Features.Work.WorkItems.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;

namespace Ipm.Application.Features.Activity.Feeds.Queries.GetWorkItemActivity;

internal sealed class GetWorkItemActivityQueryHandler(IApplicationDbContext db, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog, IAuditFeed feed)
    : IQueryHandler<GetWorkItemActivityQuery, PagedList<AuditEventResponse>>
{
    public async ValueTask<Result<PagedList<AuditEventResponse>>> Handle(GetWorkItemActivityQuery query, CancellationToken cancellationToken)
    {
        var guarded = await WorkItemGuard.ReadAsync(db, currentUser, access, securityLog, query.WorkItemId, Permissions.TasksView, cancellationToken);
        if (guarded.IsFailure)
        {
            return guarded.Error;
        }

        var projectId = guarded.Value.Item.ProjectId;
        return await feed.SearchAsync(new AuditFilter(WorkItemId: query.WorkItemId), AuditVisibility.OfProjects([projectId]), query.Page, cancellationToken);
    }
}
