using Ipm.Application.Abstractions.Messaging;

namespace Ipm.Application.Features.Identity.Roles.Commands.DeleteRole;

public sealed record DeleteRoleCommand(Guid Id, uint ExpectedVersion) : ICommand;
