using Ipm.Application.Abstractions.Auditing;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common.Pagination;
using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Config.RelationTypes.Queries.GetRelationTypeHistory;

internal sealed class GetRelationTypeHistoryQueryHandler(IApplicationDbContext db, IAuditLogReader audit)
    : IQueryHandler<GetRelationTypeHistoryQuery, PagedList<AuditRecord>>
{
    private const string Entity = "config.relation_types";

    public async ValueTask<Result<PagedList<AuditRecord>>> Handle(GetRelationTypeHistoryQuery query, CancellationToken cancellationToken)
    {
        if (!await db.RelationTypes.AnyAsync(t => t.Id == query.Id, cancellationToken))
        {
            return RelationTypeErrors.NotFound;
        }

        return await audit.ReadAsync(Entity, query.Id, query.Page, cancellationToken);
    }
}
