using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Identity.Auth.Common;

namespace Ipm.Application.Features.Identity.Auth.Commands.Login;

public sealed record LoginCommand(string Username, string Password) : ICommand<SessionResult>;
