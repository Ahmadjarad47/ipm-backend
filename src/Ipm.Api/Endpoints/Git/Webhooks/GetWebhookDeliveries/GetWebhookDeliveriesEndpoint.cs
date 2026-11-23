using Ipm.Api.Infrastructure;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Features.Git.Webhooks.Queries.GetWebhookDeliveries;
using Mediator;

namespace Ipm.Api.Endpoints.Git.Webhooks.GetWebhookDeliveries;

internal sealed class GetWebhookDeliveriesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("git/deliveries", async (ISender sender, CancellationToken ct, Guid? projectId = null, int page = 1, int pageSize = 50) =>
                (await sender.Send(new GetWebhookDeliveriesQuery(projectId, new PageRequest(page, pageSize)), ct)).ToHttpResult())
            .WithTags(GitTags.Webhooks)
            .WithSummary("سجل تسليمات الويب هوك")
            .WithDescription("projectId إلزامي، بصلاحية git.view. الأحدث أولاً.");
}
