using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Identity.Roles.Common;
using Ipm.Domain.Identity;

namespace Ipm.Application.Features.Identity.Roles.Commands.CreateRole;

/// <summary>When both permission lists are empty and <paramref name="CopyFromRoleId"/> is given, the new role starts as a copy of that role.</summary>
public sealed record CreateRoleCommand(
    string Name,
    string Description,
    RoleScope Scope,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> Denied,
    Guid? CopyFromRoleId) : ICommand<RoleResponse>;
