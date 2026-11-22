using Ipm.Application.Abstractions.Messaging;

namespace Ipm.Application.Features.Identity.Users.Commands.ResetUserPassword;

/// <summary>The administrator sets a temporary password. The user must change it at the next sign-in (R6).</summary>
public sealed record ResetUserPasswordCommand(Guid Id, string NewPassword) : ICommand;
