using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common.Pagination;

namespace Ipm.Application.Features.Identity.Users.Queries.GetUsers;

/// <summary><paramref name="Search"/> matches name, username or e-mail.</summary>
public sealed record GetUsersQuery(string? Search, bool? Active, Guid? RoleId, PageRequest Page) : IQuery<PagedList<UserListItem>>;
