using Ipm.Api.Infrastructure;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Features.Activity.Feeds.Queries.GetWorkItemActivity;
using Mediator;

namespace Ipm.Api.Endpoints.Activity.Feeds.GetWorkItemActivity;

internal sealed class GetWorkItemActivityEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("work-items/{id:guid}/activity", async (Guid id, ISender sender, CancellationToken ct, int page = 1, int pageSize = 50) =>
                (await sender.Send(new GetWorkItemActivityQuery(id, new PageRequest(page, pageSize)), ct)).ToHttpResult())
            .WithTags(ActivityTags.Activity)
            .WithSummary("نشاط عنصر العمل")
            .WithDescription("تعديلاته وتنقلاته وتعليقاته وروابطه وطلبات الموافقة عليه، الأحدث أولاً. بصلاحية tasks.view (غيرها 404/403). pageSize حتى 200.");
}
