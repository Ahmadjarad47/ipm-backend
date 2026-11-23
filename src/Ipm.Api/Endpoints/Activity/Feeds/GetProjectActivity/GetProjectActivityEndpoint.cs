using Ipm.Api.Infrastructure;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Features.Activity.Feeds.Queries.GetProjectActivity;
using Mediator;

namespace Ipm.Api.Endpoints.Activity.Feeds.GetProjectActivity;

internal sealed class GetProjectActivityEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("projects/{id:guid}/activity", async (Guid id, ISender sender, CancellationToken ct, int page = 1, int pageSize = 50) =>
                (await sender.Send(new GetProjectActivityQuery(id, new PageRequest(page, pageSize)), ct)).ToHttpResult())
            .WithTags(ActivityTags.Activity)
            .WithSummary("نشاط المشروع")
            .WithDescription("أحداث المشروع وكل ما فيه، الأحدث أولاً، لمن بيشوف المشروع (غيره 404). أحداث الأمان ما بتظهر هون. pageSize حتى 200.");
}
