using Ipm.Application.Abstractions.Messaging;

namespace Ipm.Application.Features.Identity.Auth.Commands.Logout;

/// <summary>Revokes the refresh token of the current session, if the cookie was sent.</summary>
public sealed record LogoutCommand(string? RefreshToken) : ICommand;
