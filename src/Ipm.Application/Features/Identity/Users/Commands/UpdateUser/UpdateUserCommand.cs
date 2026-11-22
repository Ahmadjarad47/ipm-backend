using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Identity.Users.Common;

namespace Ipm.Application.Features.Identity.Users.Commands.UpdateUser;

/// <summary>Profile only: roles have their own command (they carry the privilege rules).</summary>
public sealed record UpdateUserCommand(
    Guid Id,
    string Name,
    string Email,
    string Title,
    Guid? ManagerId,
    uint ExpectedVersion) : ICommand<UserResponse>;
