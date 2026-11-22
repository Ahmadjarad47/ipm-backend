using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Identity.Users.Common;

namespace Ipm.Application.Features.Identity.Users.Commands.CreateUser;

/// <summary>The administrator chooses the first password; the user must change it at first sign-in (R6).</summary>
public sealed record CreateUserCommand(
    string Username,
    string Name,
    string Email,
    string Title,
    Guid? ManagerId,
    IReadOnlyList<Guid> SystemRoleIds,
    string Password) : ICommand<UserResponse>;
