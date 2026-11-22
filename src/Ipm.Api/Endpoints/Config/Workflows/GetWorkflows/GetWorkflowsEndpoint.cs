using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Config.Workflows.Queries.GetWorkflows;
using Ipm.Domain.Config;
using Mediator;

namespace Ipm.Api.Endpoints.Config.Workflows.GetWorkflows;

internal sealed class GetWorkflowsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("workflows", async (ISender sender, CancellationToken ct, string? scope = null) =>
            {
                var parsed = KebabCaseEnum.ParseQuery<StatusScope>(scope, "scope", out var problem);
                return problem ?? (await sender.Send(new GetWorkflowsQuery(parsed), ct)).ToHttpResult();
            })
            .WithTags(ConfigTags.Workflows)
            .WithSummary("سير العمل بحالاته وانتقالاته")
            .WithDescription("scope: project أو work-item. أي انتقال غير مذكور ممنوع.");
}
