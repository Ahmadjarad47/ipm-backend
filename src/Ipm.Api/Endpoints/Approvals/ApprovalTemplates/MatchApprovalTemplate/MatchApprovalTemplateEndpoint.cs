using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Approvals.ApprovalTemplates.Queries.MatchApprovalTemplate;
using Mediator;

namespace Ipm.Api.Endpoints.Approvals.ApprovalTemplates.MatchApprovalTemplate;

internal sealed class MatchApprovalTemplateEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("approval-templates/match", async (string subjectType, Guid subjectId, string action, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new MatchApprovalTemplateQuery(subjectType, subjectId, action), ct);

                // No template governs the action: the answer is the JSON null, not an empty body.
                return result is { IsSuccess: true, Value: null } ? Results.Content("null", "application/json") : result.ToHttpResult();
            })
            .WithTags(ApprovalsTags.Templates)
            .WithSummary("القالب الذي يحكم إجراءً على عمل")
            .WithDescription("subjectType: project أو work-item. action: close أو reopen أو change أو activate. الأعلى rule.priority ثم الأكثر تحديداً ثم الأقدم. بيرجّع null إذا ما في قالب فعّال بينطبق.");
}
