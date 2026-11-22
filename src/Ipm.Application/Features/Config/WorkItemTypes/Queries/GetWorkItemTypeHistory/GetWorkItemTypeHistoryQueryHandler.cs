using Ipm.Application.Abstractions.Auditing;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common.Pagination;
using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Config.WorkItemTypes.Queries.GetWorkItemTypeHistory;

internal sealed class GetWorkItemTypeHistoryQueryHandler(IApplicationDbContext db, IAuditLogReader audit)
    : IQueryHandler<GetWorkItemTypeHistoryQuery, PagedList<AuditRecord>>
{
    private const string Entity = "config.work_item_types";

    public async ValueTask<Result<PagedList<AuditRecord>>> Handle(GetWorkItemTypeHistoryQuery query, CancellationToken cancellationToken)
    {
        if (!await db.WorkItemTypes.AnyAsync(t => t.Id == query.Id, cancellationToken))
        {
            return WorkItemTypeErrors.NotFound;
        }

        return await audit.ReadAsync(Entity, query.Id, query.Page, cancellationToken);
    }
}
