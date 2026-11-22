using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Identity.Roles.Common;
using Ipm.Domain.Identity;

namespace Ipm.Application.Features.Identity.Roles.Commands.UpdateRole;

/// <summary><paramref name="Scope"/> is optional and only checked: it can never change after creation (R15).</summary>
public sealed record UpdateRoleCommand(
    Guid Id,
    string Name,
    string Description,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> Denied,
    RoleScope? Scope,
    uint ExpectedVersion) : ICommand<RoleResponse>;
