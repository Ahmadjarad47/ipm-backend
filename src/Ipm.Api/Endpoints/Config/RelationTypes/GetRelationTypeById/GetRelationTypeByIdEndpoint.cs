using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Config.RelationTypes.Queries.GetRelationTypeById;
using Mediator;

namespace Ipm.Api.Endpoints.Config.RelationTypes.GetRelationTypeById;

internal sealed class GetRelationTypeByIdEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("relation-types/{id:guid}", async (Guid id, ISender sender, HttpResponse response, CancellationToken ct) =>
                (await sender.Send(new GetRelationTypeByIdQuery(id), ct)).ToETagResult(response, t => t.Version))
            .WithTags(ConfigTags.RelationTypes)
            .WithSummary("نوع رابط واحد (ETag)");
}
