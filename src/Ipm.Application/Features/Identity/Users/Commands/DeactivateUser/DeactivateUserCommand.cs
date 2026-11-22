using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Identity.Users.Common;

namespace Ipm.Application.Features.Identity.Users.Commands.DeactivateUser;

public sealed record DeactivateUserCommand(Guid Id, uint ExpectedVersion) : ICommand<UserResponse>;
