using Ipm.Application.Abstractions.Authentication;

namespace Ipm.Application.Features.Identity.Auth.Common;

/// <summary>Result of login, refresh and password change: new tokens plus whether a password change is required.</summary>
public sealed record SessionResult(AuthTokens Tokens, bool MustChangePassword);
