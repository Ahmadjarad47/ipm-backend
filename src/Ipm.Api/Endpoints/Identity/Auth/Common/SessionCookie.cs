using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Identity.Auth.Common;
using Ipm.Domain.Common;

namespace Ipm.Api.Endpoints.Identity.Auth.Common;

/// <summary>
/// The refresh token cookie (HttpOnly, Secure, SameSite=Strict, only sent to /api/v1/auth) and the shared
/// response of login, refresh and password change.
/// </summary>
internal static class SessionCookie
{
    /// <summary>Rate limiting policy of sign-in and refresh (R1, spec 01 §5).</summary>
    public const string RateLimitPolicy = "auth";

    public const string Name = "ipm_refresh";
    private const string Path = "/api/v1/auth";

    public static string? Read(HttpRequest request) => request.Cookies[Name];

    /// <summary>On success: sets the cookie and returns the access token; on failure: the problem.</summary>
    public static IResult ToSessionResult(this Result<SessionResult> result, HttpResponse response)
    {
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        var tokens = result.Value.Tokens;
        response.Cookies.Append(Name, tokens.RefreshToken, Options(tokens.RefreshTokenExpiresAt));
        return Results.Ok(new SessionResponse(tokens.AccessToken, tokens.AccessTokenExpiresAt, result.Value.MustChangePassword));
    }

    public static void Delete(HttpResponse response) => response.Cookies.Delete(Name, Options(DateTimeOffset.UnixEpoch));

    private static CookieOptions Options(DateTimeOffset expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = Path,
        Expires = expires,
        IsEssential = true,
    };
}
