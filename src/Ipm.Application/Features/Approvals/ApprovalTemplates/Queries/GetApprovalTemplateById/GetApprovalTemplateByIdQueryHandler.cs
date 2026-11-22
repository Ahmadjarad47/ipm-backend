using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Approvals.ApprovalTemplates.Common;
using Ipm.Domain.Approvals;
using Ipm.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Approvals.ApprovalTemplates.Queries.GetApprovalTemplateById;

internal sealed class GetApprovalTemplateByIdQueryHandler(IApplicationDbContext db) : IQueryHandler<GetApprovalTemplateByIdQuery, ApprovalTemplateResponse>
{
    public async ValueTask<Result<ApprovalTemplateResponse>> Handle(GetApprovalTemplateByIdQuery query, CancellationToken cancellationToken)
    {
        var template = await db.ApprovalTemplates.AsNoTracking().SingleOrDefaultAsync(t => t.Id == query.Id, cancellationToken);
        return template is null ? ApprovalErrors.TemplateNotFound : template.ToResponse();
    }
}
