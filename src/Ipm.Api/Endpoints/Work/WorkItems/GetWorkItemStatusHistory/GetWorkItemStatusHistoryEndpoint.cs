using Ipm.Api.Infrastructure;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Features.Work.WorkItems.Queries.GetWorkItemStatusHistory;
using Mediator;

namespace Ipm.Api.Endpoints.Work.WorkItems.GetWorkItemStatusHistory;

internal sealed class GetWorkItemStatusHistoryEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("work-items/{id:guid}/status-history", async (Guid id, ISender sender, CancellationToken ct, int page = 1, int pageSize = 50) =>
                (await sender.Send(new GetWorkItemStatusHistoryQuery(id, new PageRequest(page, pageSize)), ct)).ToHttpResult())
            .WithTags(WorkTags.WorkItems)
            .WithSummary("سجل حالات العمل (الأحدث أولاً)")
            .WithDescription("من أي حالة لأي حالة، ومن نفّذ ومتى والتعليق، وهل نُفّذ بتجاوز الموافقة. pageSize حتى 200.");
}
