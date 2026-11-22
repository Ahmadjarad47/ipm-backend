using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Approvals.Approvals.Commands.BulkDecideApprovals;
using Mediator;

namespace Ipm.Api.Endpoints.Approvals.Approvals.BulkDecideApprovals;

internal sealed class BulkDecideApprovalsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("approvals/bulk-decide", async (BulkDecideApprovalsRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new BulkDecideApprovalsCommand(r.Ids ?? [], r.Comment, r.Revisions ?? []), ct)).ToHttpResult())
            .WithTags(ApprovalsTags.Requests)
            .WithSummary("موافقة جماعية (حتى 50)")
            .WithDescription("موافقة فقط. revisions: لكل طلب رقم الإصدار اللي راجعته. النتيجة لكل طلب { id, ok, message } وطلب مرفوض ما بيوقف الباقي.");
}
