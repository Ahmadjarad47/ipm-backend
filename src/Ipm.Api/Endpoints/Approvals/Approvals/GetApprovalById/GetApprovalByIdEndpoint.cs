using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Approvals.Approvals.Queries.GetApprovalById;
using Mediator;

namespace Ipm.Api.Endpoints.Approvals.Approvals.GetApprovalById;

internal sealed class GetApprovalByIdEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("approvals/{id:guid}", async (Guid id, ISender sender, HttpResponse response, CancellationToken ct) =>
                (await sender.Send(new GetApprovalByIdQuery(id), ct)).ToETagResult(response, a => a.Version))
            .WithTags(ApprovalsTags.Requests)
            .WithSummary("طلب موافقة واحد (ETag)")
            .WithDescription("للمقدّم أو المعتمد الحالي أو من معه approvals.view بالمشروع. غيرهم ← 404.");
}
