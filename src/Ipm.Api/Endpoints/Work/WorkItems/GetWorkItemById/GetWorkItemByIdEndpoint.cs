using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Work.WorkItems.Queries.GetWorkItemById;
using Mediator;

namespace Ipm.Api.Endpoints.Work.WorkItems.GetWorkItemById;

internal sealed class GetWorkItemByIdEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("work-items/{id:guid}", async (Guid id, ISender sender, HttpResponse response, CancellationToken ct) =>
                (await sender.Send(new GetWorkItemByIdQuery(id), ct)).ToETagResult(response, i => i.Version))
            .WithTags(WorkTags.WorkItems)
            .WithSummary("عنصر عمل واحد (ETag)")
            .WithDescription("بصلاحية tasks.view داخل المشروع. عنصر بمشروع ما بتراه بيرجّع 404.");
}
