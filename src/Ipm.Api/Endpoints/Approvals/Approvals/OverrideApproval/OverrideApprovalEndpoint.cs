using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Approvals.Approvals.Commands.OverrideApproval;
using Mediator;

namespace Ipm.Api.Endpoints.Approvals.Approvals.OverrideApproval;

internal sealed class OverrideApprovalEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("approvals/{id:guid}/override", async (Guid id, OverrideApprovalRequest r, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                if (http.Request.ReadIfMatch(out var problem) is not { } version)
                {
                    return problem!;
                }

                return (await sender.Send(new OverrideApprovalCommand(id, r.Decision, r.Comment, r.Revision, version), ct)).ToETagResult(http.Response, a => a.Version);
            })
            .WithTags(ApprovalsTags.Requests)
            .WithSummary("قرار مباشر يتخطى المراحل (If-Match)")
            .WithDescription("decision: approve أو reject فقط، والتعليق (السبب) إلزامي. بصلاحية audit.override (مدير النظام)، وبيحسم الطلب فوراً ويتخطى المراحل المتبقية، حتى لو المستخدم هو المقدِّم. revision هو الإصدار اللي راجعته (وإلا 409). القرار بيتسجّل بالمسار والتدقيق.");
}
