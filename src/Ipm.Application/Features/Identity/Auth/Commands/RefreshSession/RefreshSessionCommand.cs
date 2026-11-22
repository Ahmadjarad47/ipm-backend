using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Identity.Auth.Common;

namespace Ipm.Application.Features.Identity.Auth.Commands.RefreshSession;

/// <summary>Exchanges a refresh token (from the HttpOnly cookie) for a new session.</summary>
public sealed record RefreshSessionCommand(string RefreshToken) : ICommand<SessionResult>;
