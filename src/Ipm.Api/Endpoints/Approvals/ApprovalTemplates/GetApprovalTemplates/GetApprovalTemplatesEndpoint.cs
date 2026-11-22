using Ipm.Api.Infrastructure;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Features.Approvals.ApprovalTemplates.Queries.GetApprovalTemplates;
using Mediator;

namespace Ipm.Api.Endpoints.Approvals.ApprovalTemplates.GetApprovalTemplates;

internal sealed class GetApprovalTemplatesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("approval-templates", async (ISender sender, CancellationToken ct, bool? active = null, int page = 1, int pageSize = 50) =>
                (await sender.Send(new GetApprovalTemplatesQuery(active, new PageRequest(page, pageSize)), ct)).ToHttpResult())
            .WithTags(ApprovalsTags.Templates)
            .WithSummary("قوالب الموافقة بالاسم")
            .WithDescription("لأي مسجّل (لازم لشاشة التقديم). active=true للفعّالة فقط. pageSize حتى 200.");
}
