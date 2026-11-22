using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Approvals.Approvals.Commands.ResubmitApproval;
using Mediator;

namespace Ipm.Api.Endpoints.Approvals.Approvals.ResubmitApproval;

internal sealed class ResubmitApprovalEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("approvals/{id:guid}/resubmit", async (Guid id, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                if (http.Request.ReadIfMatch(out var problem) is not { } version)
                {
                    return problem!;
                }

                return (await sender.Send(new ResubmitApprovalCommand(id, version), ct)).ToETagResult(http.Response, a => a.Version);
            })
            .WithTags(ApprovalsTags.Requests)
            .WithSummary("إعادة تقديم طلب مُعاد أو مُبطل (If-Match)")
            .WithDescription("لمقدّم الطلب فقط. بيزيد الإصدار ويبدأ من المرحلة 1 بلقطة ومعتمدين جدد.");
}
