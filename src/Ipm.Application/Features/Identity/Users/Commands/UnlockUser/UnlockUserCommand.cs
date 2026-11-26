using Ipm.Application.Abstractions.Messaging;

namespace Ipm.Application.Features.Identity.Users.Commands.UnlockUser;

/// <summary>An administrator lifts the sign-in lockout of a user now, instead of waiting for it to expire.</summary>
public sealed record UnlockUserCommand(Guid Id) : ICommand;
