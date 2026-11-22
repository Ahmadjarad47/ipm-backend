using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Config.WorkItemTypes.Queries.GetWorkItemTypeById;
using Mediator;

namespace Ipm.Api.Endpoints.Config.WorkItemTypes.GetWorkItemTypeById;

internal sealed class GetWorkItemTypeByIdEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("work-item-types/{id:guid}", async (Guid id, ISender sender, HttpResponse response, CancellationToken ct) =>
                (await sender.Send(new GetWorkItemTypeByIdQuery(id), ct)).ToETagResult(response, t => t.Version))
            .WithTags(ConfigTags.WorkItemTypes)
            .WithSummary("نوع عمل واحد (ETag)");
}
