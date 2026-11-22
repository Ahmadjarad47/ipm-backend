using Ipm.Api.Endpoints.Identity.Auth.Common;
using Ipm.Application.Features.Identity.Auth.Commands.Login;
using Mediator;

namespace Ipm.Api.Endpoints.Identity.Auth.Login;

internal sealed class LoginEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("auth/login", async (LoginRequest request, ISender sender, HttpResponse response, CancellationToken ct) =>
                (await sender.Send(new LoginCommand(request.Username, request.Password), ct)).ToSessionResult(response))
            .AllowAnonymous()
            .RequireRateLimiting(SessionCookie.RateLimitPolicy)
            .WithTags(IdentityTags.Auth)
            .WithSummary("تسجيل الدخول باسم المستخدم وكلمة المرور")
            .WithDescription("يرجع access token (15 دقيقة) في الجسم، و refresh token (7 أيام) في cookie HttpOnly. القفل بعد 5 محاولات خاطئة (429).");
}
