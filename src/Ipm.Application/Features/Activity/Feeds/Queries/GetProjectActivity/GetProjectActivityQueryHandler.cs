using Ipm.Application.Abstractions.Auditing;
using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Features.Projects.Projects.Common;
using Ipm.Domain.Common;

namespace Ipm.Application.Features.Activity.Feeds.Queries.GetProjectActivity;

internal sealed class GetProjectActivityQueryHandler(IApplicationDbContext db, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog, IAuditFeed feed)
    : IQueryHandler<GetProjectActivityQuery, PagedList<AuditEventResponse>>
{
    public async ValueTask<Result<PagedList<AuditEventResponse>>> Handle(GetProjectActivityQuery query, CancellationToken cancellationToken)
    {
        var guarded = await ProjectGuard.ReadAsync(db, currentUser, access, securityLog, query.ProjectId, cancellationToken);
        if (guarded.IsFailure)
        {
            return guarded.Error;
        }

        return await feed.SearchAsync(new AuditFilter(ProjectId: query.ProjectId), AuditVisibility.OfProjects([query.ProjectId]), query.Page, cancellationToken);
    }
}
