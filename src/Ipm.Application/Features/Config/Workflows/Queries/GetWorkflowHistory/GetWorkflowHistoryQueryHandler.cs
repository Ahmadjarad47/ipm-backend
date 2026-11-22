using Ipm.Application.Abstractions.Auditing;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common.Pagination;
using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Config.Workflows.Queries.GetWorkflowHistory;

internal sealed class GetWorkflowHistoryQueryHandler(IApplicationDbContext db, IAuditLogReader audit)
    : IQueryHandler<GetWorkflowHistoryQuery, PagedList<AuditRecord>>
{
    private const string Entity = "config.workflows";

    public async ValueTask<Result<PagedList<AuditRecord>>> Handle(GetWorkflowHistoryQuery query, CancellationToken cancellationToken)
    {
        if (!await db.Workflows.AnyAsync(w => w.Id == query.Id, cancellationToken))
        {
            return WorkflowErrors.NotFound;
        }

        return await audit.ReadAsync(Entity, query.Id, query.Page, cancellationToken);
    }
}
