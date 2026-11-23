using Ipm.Application.Abstractions.Auditing;
using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Features.Activity.Common;
using Ipm.Application.Features.Identity.Common;
using Ipm.Domain.Common;

namespace Ipm.Application.Features.Activity.Audit.Queries.GetAuditEvents;

internal sealed class GetAuditEventsQueryHandler(IApplicationDbContext db, IUserContext currentUser, IAccessService access, IAuditFeed feed)
    : IQueryHandler<GetAuditEventsQuery, PagedList<AuditEventResponse>>
{
    public async ValueTask<Result<PagedList<AuditEventResponse>>> Handle(GetAuditEventsQuery query, CancellationToken cancellationToken)
    {
        var caller = await AccessChecks.CallerAsync(currentUser, access, cancellationToken);
        if (caller.IsFailure)
        {
            return caller.Error;
        }

        var visibility = await AuditAccess.VisibilityAsync(db, caller.Value, cancellationToken);
        var filter = new AuditFilter(
            AuditAccess.StartOf(query.From), AuditAccess.EndOf(query.To), query.ActorId, query.Action, query.SubjectType, query.SubjectId, query.ProjectId,
            WorkItemId: null, query.Source, query.CorrelationId, query.Security, query.Search);
        return await feed.SearchAsync(filter, visibility, query.Page, cancellationToken);
    }
}
