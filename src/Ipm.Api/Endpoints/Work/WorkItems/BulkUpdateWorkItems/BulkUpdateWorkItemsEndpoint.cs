using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Work.WorkItems.Commands.BulkUpdateWorkItems;
using Mediator;

namespace Ipm.Api.Endpoints.Work.WorkItems.BulkUpdateWorkItems;

internal sealed class BulkUpdateWorkItemsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("work-items/bulk", async (BulkUpdateWorkItemsRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new BulkUpdateWorkItemsCommand(r.Ids ?? [], r.Action), ct)).ToHttpResult())
            .WithTags(WorkTags.WorkItems)
            .WithSummary("تعديل جماعي للأعمال (حتى 100)")
            .WithDescription("action.kind: assign (assigneeIds) أو priority (priority) أو dueDate (dueDate) أو transition (toStatusId, comment). كل عمل بيتفحص بقواعده وصلاحياته لحاله، والرد نتيجة لكل عمل، وعمل مرفوض ما بيوقف الباقي.");
}
