using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common.Pagination;
using Ipm.Domain.Identity;

namespace Ipm.Application.Features.Identity.Roles.Queries.GetRoles;

public sealed record GetRolesQuery(RoleScope? Scope, PageRequest Page) : IQuery<PagedList<RoleListItem>>;
