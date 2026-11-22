using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Identity.Users.Common;

namespace Ipm.Application.Features.Identity.Users.Commands.ActivateUser;

public sealed record ActivateUserCommand(Guid Id, uint ExpectedVersion) : ICommand<UserResponse>;
