using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Config.Statuses.Queries.GetStatusById;
using Mediator;

namespace Ipm.Api.Endpoints.Config.Statuses.GetStatusById;

internal sealed class GetStatusByIdEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("statuses/{id:guid}", async (Guid id, ISender sender, HttpResponse response, CancellationToken ct) =>
                (await sender.Send(new GetStatusByIdQuery(id), ct)).ToETagResult(response, s => s.Version))
            .WithTags(ConfigTags.Statuses)
            .WithSummary("حالة واحدة (ETag)");
}
