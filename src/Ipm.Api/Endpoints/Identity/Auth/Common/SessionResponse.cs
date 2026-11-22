namespace Ipm.Api.Endpoints.Identity.Auth.Common;

/// <summary>The access token goes in the body (keep it in memory); the refresh token only ever travels in an HttpOnly cookie.</summary>
public sealed record SessionResponse(string AccessToken, DateTimeOffset ExpiresAt, bool MustChangePassword);
