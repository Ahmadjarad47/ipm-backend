using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Identity.Users.Common;

namespace Ipm.Application.Features.Identity.Users.Commands.SetUserRoles;

public sealed record SetUserRolesCommand(Guid Id, IReadOnlyList<Guid> SystemRoleIds, uint ExpectedVersion) : ICommand<UserResponse>;
