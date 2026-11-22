using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Identity.Roles.Queries.GetRoleById;
using Mediator;

namespace Ipm.Api.Endpoints.Identity.Roles.GetRoleById;

internal sealed class GetRoleByIdEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("roles/{id:guid}", async (Guid id, ISender sender, HttpResponse response, CancellationToken ct) =>
                (await sender.Send(new GetRoleByIdQuery(id), ct)).ToETagResult(response, r => r.Version))
            .WithTags(IdentityTags.Roles)
            .WithSummary("دور واحد مع صلاحياته والممنوع (ETag)");
}
