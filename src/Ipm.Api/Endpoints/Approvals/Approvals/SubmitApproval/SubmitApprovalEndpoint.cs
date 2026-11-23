using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Approvals.Approvals.Commands.SubmitApproval;
using Mediator;

namespace Ipm.Api.Endpoints.Approvals.Approvals.SubmitApproval;

internal sealed class SubmitApprovalEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("approvals", async (SubmitApprovalRequest r, ISender sender, HttpResponse response, CancellationToken ct) =>
                (await sender.Send(new SubmitApprovalCommand(r.TemplateId, r.SubjectType, r.SubjectId, r.Action, r.TargetStatusId, r.ProposedChanges), ct))
                    .ToCreatedETagResult(response, a => a.Version, a => $"/api/v1/approvals/{a.Id}"))
            .WithTags(ApprovalsTags.Requests)
            .WithSummary("تقديم طلب موافقة")
            .WithDescription("بصلاحية approvals.request بالمشروع. subjectType: project أو work-item. القالب لازم يكون فعّالاً وبينطبق على العنصر والإجراء (action: close أو reopen أو activate، والمُستنتج إذا غاب)، وكل مرحلة إلها معتمد. بيتجمّد المعتمدون ولقطة الحقول المحمية. طلب معلّق لنفس العنصر والقالب ← 409.");
}
