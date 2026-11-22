using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Identity.Users.Commands.ActivateUser;
using Ipm.Domain.Identity;
using Mediator;

namespace Ipm.Api.Endpoints.Identity.Users.ActivateUser;

internal sealed class ActivateUserEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("users/{id:guid}/activate", async (Guid id, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                if (http.Request.ReadIfMatch(out var problem) is not { } version)
                {
                    return problem!;
                }

                return (await sender.Send(new ActivateUserCommand(id, version), ct)).ToETagResult(http.Response, u => u.Version);
            })
            .RequireAuthorization(Permissions.PermissionsAssign)
            .WithTags(IdentityTags.Users)
            .WithSummary("تفعيل مستخدم (If-Match)");
}
