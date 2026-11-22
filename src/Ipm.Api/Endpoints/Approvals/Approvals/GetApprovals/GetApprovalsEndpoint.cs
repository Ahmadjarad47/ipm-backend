using Ipm.Api.Infrastructure;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Features.Approvals.Approvals.Queries.GetApprovals;
using Mediator;

namespace Ipm.Api.Endpoints.Approvals.Approvals.GetApprovals;

internal sealed class GetApprovalsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("approvals", async (
                    ISender sender,
                    CancellationToken ct,
                    string? box = null,
                    bool overdue = false,
                    string? status = null,
                    Guid? projectId = null,
                    string? subjectType = null,
                    Guid? subjectId = null,
                    Guid? templateId = null,
                    int page = 1,
                    int pageSize = 50) =>
                (await sender.Send(new GetApprovalsQuery(box, overdue, status, projectId, subjectType, subjectId, templateId, new PageRequest(page, pageSize)), ct)).ToHttpResult())
            .WithTags(ApprovalsTags.Requests)
            .WithSummary("طلبات الموافقة حسب الصندوق")
            .WithDescription("box: inbox (اللي تقدر تقرّر عليها هلق، الافتراضي) أو sent (اللي قدّمتها) أو closed (غير المعلّقة وإنت طرف فيها). status: pending-approval أو approved أو rejected أو returned أو superseded. overdue=true للمتأخرة. pageSize حتى 200.");
}
