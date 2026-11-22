using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Identity.Auth.Common;

namespace Ipm.Application.Features.Identity.Auth.Commands.ChangeMyPassword;

/// <summary>Ends every other session and returns fresh tokens so the current one continues.</summary>
public sealed record ChangeMyPasswordCommand(string CurrentPassword, string NewPassword) : ICommand<SessionResult>;
