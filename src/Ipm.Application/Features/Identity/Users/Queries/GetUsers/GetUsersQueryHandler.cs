using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common.Pagination;
using Ipm.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Identity.Users.Queries.GetUsers;

internal sealed class GetUsersQueryHandler(IApplicationDbContext db) : IQueryHandler<GetUsersQuery, PagedList<UserListItem>>
{
    public async ValueTask<Result<PagedList<UserListItem>>> Handle(GetUsersQuery query, CancellationToken cancellationToken)
    {
        var users = db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // Names and titles are Arabic (no letter case); username and e-mail match on their normalized upper-case form.
            var term = query.Search.Trim();
            var upper = term.ToUpperInvariant();
            users = users.Where(u => u.Name.Contains(term) || u.NormalizedUserName!.Contains(upper) || u.NormalizedEmail!.Contains(upper));
        }

        if (query.Active is { } active)
        {
            users = users.Where(u => u.IsActive == active);
        }

        if (query.RoleId is { } roleId)
        {
            users = users.Where(u => u.RoleIds.Contains(roleId));
        }

        var total = await users.CountAsync(cancellationToken);
        var items = await users
            .OrderBy(u => u.Name)
            .Skip(query.Page.Skip)
            .Take(query.Page.Take)
            .Select(u => new UserListItem(u.Id, u.UserName!, u.Name, u.Email!, u.Title, u.IsActive, u.ManagerId, u.RoleIds, u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow))
            .ToListAsync(cancellationToken);

        return new PagedList<UserListItem>(items, Math.Max(query.Page.Page, 1), query.Page.Take, total);
    }
}
