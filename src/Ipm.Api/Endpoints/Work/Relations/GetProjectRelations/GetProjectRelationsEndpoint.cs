using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Work.Relations.Queries.GetProjectRelations;
using Mediator;

namespace Ipm.Api.Endpoints.Work.Relations.GetProjectRelations;

internal sealed class GetProjectRelationsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("projects/{id:guid}/relations", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetProjectRelationsQuery(id), ct)).ToHttpResult())
            .WithTags(WorkTags.Relations)
            .WithSummary("روابط المشروع")
            .WithDescription("بصلاحية workitem.relations.view. الروابط اللي طرفاها داخل المشروع وغير مؤرشفين.");
}
