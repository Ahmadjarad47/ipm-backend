using Ipm.Api.Infrastructure;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Features.Projects.Projects.Queries.GetProjectStatusHistory;
using Mediator;

namespace Ipm.Api.Endpoints.Projects.Projects.GetProjectStatusHistory;

internal sealed class GetProjectStatusHistoryEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("projects/{id:guid}/status-history", async (Guid id, ISender sender, CancellationToken ct, int page = 1, int pageSize = 50) =>
                (await sender.Send(new GetProjectStatusHistoryQuery(id, new PageRequest(page, pageSize)), ct)).ToHttpResult())
            .WithTags(ProjectsTags.Projects)
            .WithSummary("سجل حالات المشروع (الأحدث أولاً)")
            .WithDescription("من أي حالة لأي حالة، ومن نفّذ ومتى والتعليق، وهل نُفّذ بتجاوز الموافقة. pageSize حتى 200.");
}
