using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Identity.Users.Common;

namespace Ipm.Application.Features.Identity.Users.Queries.GetUserById;

public sealed record GetUserByIdQuery(Guid Id) : IQuery<UserResponse>;
