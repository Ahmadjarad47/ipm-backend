using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Config.WorkItemTypes.Queries.GetWorkItemTypes;
using Mediator;

namespace Ipm.Api.Endpoints.Config.WorkItemTypes.GetWorkItemTypes;

internal sealed class GetWorkItemTypesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("work-item-types", async (ISender sender, CancellationToken ct, bool? active = null) =>
                (await sender.Send(new GetWorkItemTypesQuery(active), ct)).ToHttpResult())
            .WithTags(ConfigTags.WorkItemTypes)
            .WithSummary("أنواع العمل بحقولها وأبنائها المسموحين")
            .WithDescription("بترتيب order. active=true للأنواع المفعّلة فقط. بدون ترقيم (قائمة إعدادات قصيرة).");
}
