using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Features.Approvals.ApprovalTemplates.Common;
using Ipm.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Approvals.ApprovalTemplates.Queries.GetApprovalTemplates;

internal sealed class GetApprovalTemplatesQueryHandler(IApplicationDbContext db) : IQueryHandler<GetApprovalTemplatesQuery, PagedList<ApprovalTemplateResponse>>
{
    public async ValueTask<Result<PagedList<ApprovalTemplateResponse>>> Handle(GetApprovalTemplatesQuery query, CancellationToken cancellationToken)
    {
        var templates = db.ApprovalTemplates.AsNoTracking().AsQueryable();
        if (query.Active is { } active)
        {
            templates = templates.Where(t => t.IsActive == active);
        }

        var total = await templates.CountAsync(cancellationToken);
        var rows = await templates.OrderBy(t => t.Name).ThenBy(t => t.Id).Skip(query.Page.Skip).Take(query.Page.Take).ToListAsync(cancellationToken);
        return new PagedList<ApprovalTemplateResponse>([.. rows.Select(t => t.ToResponse())], Math.Max(query.Page.Page, 1), query.Page.Take, total);
    }
}
