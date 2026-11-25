using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Work.Relations.Queries.GetWorkItemRelations;
using Mediator;

namespace Ipm.Api.Endpoints.Work.Relations.GetWorkItemRelations;

internal sealed class GetWorkItemRelationsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("work-items/{id:guid}/relations", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetWorkItemRelationsQuery(id), ct)).ToHttpResult())
            .WithTags(WorkTags.Relations)
            .WithSummary("روابط عمل من الجهتين")
            .WithDescription("بصلاحية workitem.relations.view. كل رابط باسمه من جهة هالعمل (الاسم للصادر والاسم العكسي للوارد) والعمل الثاني. الرابط اللي طرفه الثاني ما بتشوفه ما بيظهر.");
}
