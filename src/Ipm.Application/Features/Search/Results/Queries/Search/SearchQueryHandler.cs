using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Identity.Common;
using Ipm.Application.Features.Search.Results.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Search.Results.Queries.Search;

// EF cannot translate Contains(text, StringComparison); ToLower().Contains is what it turns into lower(col) LIKE (the database lower-cases, not the app culture).
#pragma warning disable CA1862, CA1304, CA1311
internal sealed class SearchQueryHandler(IApplicationDbContext db, IUserContext currentUser, IAccessService access) : IQueryHandler<SearchQuery, IReadOnlyList<SearchHit>>
{
    public const int MaxLimit = 30;

    public async ValueTask<Result<IReadOnlyList<SearchHit>>> Handle(SearchQuery query, CancellationToken cancellationToken)
    {
        var text = (query.Q ?? string.Empty).Trim();
        if (text.Length < 2)
        {
            return new List<SearchHit>();
        }

        var caller = await AccessChecks.CallerAsync(currentUser, access, cancellationToken);
        if (caller.IsFailure)
        {
            return caller.Error;
        }

        var me = caller.Value;
        var term = text.ToLowerInvariant();
        var limit = Math.Clamp(query.Limit <= 0 ? 12 : query.Limit, 1, MaxLimit);
        // R12: percent, underscore and backslash are plain text here (EF escapes them in Contains); the match ignores case.

        var projects = await db.Projects.AsNoTracking().Select(p => new { p.Id, p.Code, p.Name, p.IsArchived }).ToListAsync(cancellationToken);
        var visible = projects.Where(p => me.Can(Permissions.ProjectsView, p.Id)).Select(p => p.Id).ToList();
        var withTasks = projects.Where(p => me.Can(Permissions.TasksView, p.Id)).Select(p => p.Id).ToList();
        var names = projects.ToDictionary(p => p.Id, p => p.Name);

        var hits = new List<SearchHit>();

        var foundProjects = await db.Projects.AsNoTracking()
            .Where(p => visible.Contains(p.Id) && (p.Name.ToLower().Contains(term) || p.Code.ToLower().Contains(term) || p.Description.ToLower().Contains(term)))
            .OrderBy(p => p.Name).Take(limit).Select(p => new { p.Id, p.Code, p.Name, p.IsArchived }).ToListAsync(cancellationToken);
        hits.AddRange(foundProjects.Select(p => new SearchHit("project", p.Id, p.Code, p.Name, p.IsArchived ? "مشروع مؤرشف" : "مشروع", $"/projects/{p.Id}")));

        var foundItems = await db.WorkItems.AsNoTracking()
            .Where(i => !i.IsArchived && withTasks.Contains(i.ProjectId) && (i.Ref.ToLower().Contains(term) || i.Title.ToLower().Contains(term)))
            .OrderBy(i => i.Ref).Take(limit).Select(i => new { i.Id, i.Ref, i.Title, i.ProjectId, i.TypeId }).ToListAsync(cancellationToken);
        hits.AddRange(foundItems.Select(i => new SearchHit("work-item", i.Id, i.Ref, i.Title, names.GetValueOrDefault(i.ProjectId, string.Empty), $"/work-items/{i.Id}", i.TypeId)));

        var candidates = await db.ApprovalRequests.AsNoTracking()
            .Where(a => a.Title.ToLower().Contains(term))
            .OrderByDescending(a => a.CreatedAt).Take(limit * 4).Select(a => new { a.Id, a.Title, a.RequesterId, a.ProjectId }).ToListAsync(cancellationToken);
        hits.AddRange(candidates
            .Where(a => a.RequesterId == me.UserId || me.Can(Permissions.ApprovalsView, a.ProjectId))
            .Take(limit)
            .Select(a => new SearchHit("approval", a.Id, null, a.Title, "طلب موافقة", $"/approvals/{a.Id}")));

        if (me.Has(Permissions.RolesManage))
        {
            var users = await db.Users.AsNoTracking()
                .Where(u => u.Name.ToLower().Contains(term) || (u.UserName != null && u.UserName.ToLower().Contains(term)) || (u.Email != null && u.Email.ToLower().Contains(term)))
                .OrderBy(u => u.Name).Take(limit).Select(u => new { u.Id, u.UserName, u.Name, u.Title }).ToListAsync(cancellationToken);
            hits.AddRange(users.Select(u => new SearchHit("user", u.Id, u.UserName, u.Name, u.Title, "/admin/access")));
        }

        // R14: the exact code first (typing ERP-104 jumps to it), then projects, items, approvals, users.
        return hits
            .OrderByDescending(h => string.Equals(h.Code, text, StringComparison.OrdinalIgnoreCase))
            .ThenBy(h => h.Kind switch { "project" => 0, "work-item" => 1, "approval" => 2, _ => 3 })
            .Take(limit)
            .ToList();
    }
}
