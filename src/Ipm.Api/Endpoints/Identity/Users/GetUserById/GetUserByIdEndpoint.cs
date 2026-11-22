using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Identity.Users.Queries.GetUserById;
using Mediator;

namespace Ipm.Api.Endpoints.Identity.Users.GetUserById;

internal sealed class GetUserByIdEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("users/{id:guid}", async (Guid id, ISender sender, HttpResponse response, CancellationToken ct) =>
                (await sender.Send(new GetUserByIdQuery(id), ct)).ToETagResult(response, u => u.Version))
            .WithTags(IdentityTags.Users)
            .WithSummary("مستخدم واحد (ETag)")
            .WithDescription("كامل لصاحب permissions.assign، وإلا مختصر بدون بريد ومدير وأدوار.");
}
