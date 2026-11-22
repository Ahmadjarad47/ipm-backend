using Ipm.Application.Abstractions.Auditing;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common.Pagination;
using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Config.Statuses.Queries.GetStatusHistory;

internal sealed class GetStatusHistoryQueryHandler(IApplicationDbContext db, IAuditLogReader audit)
    : IQueryHandler<GetStatusHistoryQuery, PagedList<AuditRecord>>
{
    private const string Entity = "config.statuses";

    public async ValueTask<Result<PagedList<AuditRecord>>> Handle(GetStatusHistoryQuery query, CancellationToken cancellationToken)
    {
        if (!await db.Statuses.AnyAsync(s => s.Id == query.Id, cancellationToken))
        {
            return StatusErrors.NotFound;
        }

        return await audit.ReadAsync(Entity, query.Id, query.Page, cancellationToken);
    }
}
