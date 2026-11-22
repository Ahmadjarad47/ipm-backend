using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Config.RelationTypes.Queries.GetRelationTypes;
using Mediator;

namespace Ipm.Api.Endpoints.Config.RelationTypes.GetRelationTypes;

internal sealed class GetRelationTypesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("relation-types", async (ISender sender, CancellationToken ct, bool? active = null) =>
                (await sender.Send(new GetRelationTypesQuery(active), ct)).ToHttpResult())
            .WithTags(ConfigTags.RelationTypes)
            .WithSummary("أنواع الروابط")
            .WithDescription("active=true للمفعّلة فقط. أزواج الأنواع المسموحة الفاضية تعني أي نوع.");
}
