using Ipm.Application.Abstractions.Messaging;
using Ipm.Domain.Identity;

namespace Ipm.Application.Features.Identity.Auth.Queries.GetPermissions;

/// <summary>The 42 permissions grouped, with their Arabic labels (for the roles screen).</summary>
public sealed record GetPermissionsQuery : IQuery<IReadOnlyList<PermissionGroup>>;
