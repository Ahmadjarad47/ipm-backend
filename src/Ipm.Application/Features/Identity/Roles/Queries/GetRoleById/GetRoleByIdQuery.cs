using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Identity.Roles.Common;

namespace Ipm.Application.Features.Identity.Roles.Queries.GetRoleById;

public sealed record GetRoleByIdQuery(Guid Id) : IQuery<RoleResponse>;
