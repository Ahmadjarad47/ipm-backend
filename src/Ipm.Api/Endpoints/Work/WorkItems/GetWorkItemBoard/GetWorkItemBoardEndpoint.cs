using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Work.WorkItems.Queries.GetWorkItemBoard;
using Mediator;

namespace Ipm.Api.Endpoints.Work.WorkItems.GetWorkItemBoard;

internal sealed class GetWorkItemBoardEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("work-items/board", async (Guid projectId, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetWorkItemBoardQuery(projectId), ct)).ToHttpResult())
            .WithTags(WorkTags.WorkItems)
            .WithSummary("لوحة الكانبان للمشروع")
            .WithDescription("كل العناصر غير المؤرشفة بشكل خفيف، مع سير العمل الفعلي لكل نوع بالمشروع، والحقول المعلَّمة showOnCard فقط. بصلاحية tasks.view.");
}
