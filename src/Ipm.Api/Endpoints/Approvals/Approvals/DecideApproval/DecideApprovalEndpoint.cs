using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Approvals.Approvals.Commands.DecideApproval;
using Mediator;

namespace Ipm.Api.Endpoints.Approvals.Approvals.DecideApproval;

internal sealed class DecideApprovalEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("approvals/{id:guid}/decide", async (Guid id, DecideApprovalRequest r, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                if (http.Request.ReadIfMatch(out var problem) is not { } version)
                {
                    return problem!;
                }

                return (await sender.Send(new DecideApprovalCommand(id, r.Decision, r.Comment, r.Revision, version), ct)).ToETagResult(http.Response, a => a.Version);
            })
            .WithTags(ApprovalsTags.Requests)
            .WithSummary("قرار على المرحلة الحالية (If-Match)")
            .WithDescription("decision: approve أو reject أو return. بصلاحية approvals.decide ولازم تكون معتمد المرحلة أو مفوَّضاً. revision هو الإصدار اللي راجعته (وإلا 409). الرفض والإرجاع بتعليق إلزامي، والمقدِّم ما بيقرّر على طلبه.");
}
