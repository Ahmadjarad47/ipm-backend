using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Identity.Users.Commands.DeactivateUser;
using Ipm.Domain.Identity;
using Mediator;

namespace Ipm.Api.Endpoints.Identity.Users.DeactivateUser;

internal sealed class DeactivateUserEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("users/{id:guid}/deactivate", async (Guid id, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                if (http.Request.ReadIfMatch(out var problem) is not { } version)
                {
                    return problem!;
                }

                return (await sender.Send(new DeactivateUserCommand(id, version), ct)).ToETagResult(http.Response, u => u.Version);
            })
            .RequireAuthorization(Permissions.PermissionsAssign)
            .WithTags(IdentityTags.Users)
            .WithSummary("تعطيل مستخدم (If-Match)")
            .WithDescription("ما بيقدر يسجّل دخول وجلساته بتنقطع. ممنوع تعطيل نفسك أو حساب أعلى منك أو آخر مدير أدوار.");
}
