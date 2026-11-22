using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Features.Identity.Roles.Common;
using Ipm.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Identity.Roles.Queries.GetRoles;

internal sealed class GetRolesQueryHandler(IApplicationDbContext db) : IQueryHandler<GetRolesQuery, PagedList<RoleListItem>>
{
    public async ValueTask<Result<PagedList<RoleListItem>>> Handle(GetRolesQuery query, CancellationToken cancellationToken)
    {
        var roles = db.Roles.AsNoTracking();
        if (query.Scope is { } scope)
        {
            roles = roles.Where(r => r.Scope == scope);
        }

        var total = await roles.CountAsync(cancellationToken);
        var rows = await roles
            .OrderByDescending(r => r.IsBuiltIn).ThenBy(r => r.Name)
            .Skip(query.Page.Skip)
            .Take(query.Page.Take)
            .Select(r => new
            {
                r.Id, r.Key, r.Name, r.Description, r.Scope, r.IsBuiltIn,
                PermissionCount = r.Granted.Count,
                UserCount = db.Users.Count(u => u.RoleIds.Contains(r.Id)),
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(r => new RoleListItem(r.Id, r.Key, r.Name, r.Description, RoleMapping.ScopeText(r.Scope), r.IsBuiltIn, r.PermissionCount, r.UserCount))
            .ToList();

        return new PagedList<RoleListItem>(items, Math.Max(query.Page.Page, 1), query.Page.Take, total);
    }
}
