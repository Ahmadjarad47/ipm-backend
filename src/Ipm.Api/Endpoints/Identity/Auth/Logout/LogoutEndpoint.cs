using Ipm.Api.Endpoints.Identity.Auth.Common;
using Ipm.Application.Features.Identity.Auth.Commands.Logout;
using Mediator;

namespace Ipm.Api.Endpoints.Identity.Auth.Logout;

internal sealed class LogoutEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("auth/logout", async (HttpRequest request, ISender sender, HttpResponse response, CancellationToken ct) =>
            {
                await sender.Send(new LogoutCommand(SessionCookie.Read(request)), ct);
                SessionCookie.Delete(response);
                return Results.NoContent();
            })
            .WithTags(IdentityTags.Auth)
            .WithSummary("تسجيل الخروج")
            .WithDescription("يلغي الـ refresh token الحالي.");
}
