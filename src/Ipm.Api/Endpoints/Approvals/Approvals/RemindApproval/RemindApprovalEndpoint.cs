using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Approvals.Approvals.Commands.RemindApproval;
using Mediator;

namespace Ipm.Api.Endpoints.Approvals.Approvals.RemindApproval;

internal sealed class RemindApprovalEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("approvals/{id:guid}/remind", async (Guid id, ISender sender, HttpContext http, CancellationToken ct) =>
                (await sender.Send(new RemindApprovalCommand(id), ct)).ToETagResult(http.Response, a => a.Version))
            .WithTags(ApprovalsTags.Requests)
            .WithSummary("تذكير معتمدي المرحلة الحالية")
            .WithDescription("لمقدّم الطلب أو من معه approvals.configure، لطلب معلّق، مرة بالساعة (429). الإشعار الفعلي بالجزء 08.");
}
