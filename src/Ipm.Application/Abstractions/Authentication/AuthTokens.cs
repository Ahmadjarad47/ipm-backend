namespace Ipm.Application.Abstractions.Authentication;

/// <summary>The refresh token never goes in a JSON body: the API puts it in an HttpOnly cookie.</summary>
public sealed record AuthTokens(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
