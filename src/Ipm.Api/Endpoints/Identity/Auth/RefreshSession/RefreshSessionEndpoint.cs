using Ipm.Api.Endpoints.Identity.Auth.Common;
using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Identity.Auth.Commands.RefreshSession;
using Ipm.Domain.Identity;
using Mediator;

namespace Ipm.Api.Endpoints.Identity.Auth.RefreshSession;

internal sealed class RefreshSessionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("auth/refresh", async (HttpRequest request, ISender sender, HttpResponse response, CancellationToken ct) =>
                SessionCookie.Read(request) is { Length: > 0 } token
                    ? (await sender.Send(new RefreshSessionCommand(token), ct)).ToSessionResult(response)
                    : AuthErrors.RefreshInvalid.ToProblem())
            .AllowAnonymous()
            .RequireRateLimiting(SessionCookie.RefreshRateLimitPolicy)
            .WithTags(IdentityTags.Auth)
            .WithSummary("تجديد الجلسة")
            .WithDescription("يستبدل refresh token (من الـ cookie) بواحد جديد. استعمال توكن قديم مرة ثانية يلغي كل جلسات المستخدم.");
}
