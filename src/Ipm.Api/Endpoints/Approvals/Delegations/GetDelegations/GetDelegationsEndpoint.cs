using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Approvals.Delegations.Queries.GetDelegations;
using Mediator;

namespace Ipm.Api.Endpoints.Approvals.Delegations.GetDelegations;

internal sealed class GetDelegationsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("delegations", async (ISender sender, CancellationToken ct) => (await sender.Send(new GetDelegationsQuery(), ct)).ToHttpResult())
            .WithTags(ApprovalsTags.Delegations)
            .WithSummary("تفويضاتك (منك أو إليك)")
            .WithDescription("الأحدث أولاً. من معه approvals.configure بيشوف الكل.");
}
