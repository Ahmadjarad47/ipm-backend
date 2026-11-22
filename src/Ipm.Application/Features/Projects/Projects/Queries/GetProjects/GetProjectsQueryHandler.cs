using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Common;
using Ipm.Application.Features.Identity.Common;
using Ipm.Application.Features.Projects.Projects.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Ipm.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Projects.Projects.Queries.GetProjects;

internal sealed class GetProjectsQueryHandler(IApplicationDbContext db, IUserContext currentUser, IAccessService access, TimeProvider time)
    : IQueryHandler<GetProjectsQuery, PagedList<ProjectResponse>>
{
    public async ValueTask<Result<PagedList<ProjectResponse>>> Handle(GetProjectsQuery query, CancellationToken cancellationToken)
    {
        var caller = await AccessChecks.CallerAsync(currentUser, access, cancellationToken);
        if (caller.IsFailure)
        {
            return caller.Error;
        }

        IQueryable<Project> projects = db.Projects.AsNoTracking();

        // A holder of projects.view in the system sees every project; anyone else only those whose role there grants it.
        if (!caller.Value.Has(Permissions.ProjectsView))
        {
            var visible = caller.Value.Denied.Contains(Permissions.ProjectsView)
                ? []
                : caller.Value.ProjectPermissions.Where(p => p.Value.Contains(Permissions.ProjectsView)).Select(p => p.Key).ToList();
            projects = projects.Where(p => visible.Contains(p.Id));
        }

        projects = Filter(projects, query);

        var total = await projects.CountAsync(cancellationToken);
        var rows = await Sort(projects, query)
            .Include(p => p.Members)
            .Skip(query.Page.Skip)
            .Take(query.Page.Take)
            .ToListAsync(cancellationToken);

        var stats = await ProjectWorkStats.LoadAsync(db, [.. rows.Select(p => p.Id)], RiyadhClock.Today(time), cancellationToken);
        return new PagedList<ProjectResponse>([.. rows.Select(p => p.ToResponse().With(stats))], Math.Max(query.Page.Page, 1), query.Page.Take, total);
    }

    private static IQueryable<Project> Filter(IQueryable<Project> projects, GetProjectsQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // Codes are stored in upper case; names are Arabic or Latin text matched as typed.
            var term = query.Search.Trim();
            var code = Project.NormalizeCode(term);
            projects = projects.Where(p => p.Code.Contains(code) || p.Name.Contains(term));
        }

        if (query.StatusId is { } statusId)
        {
            projects = projects.Where(p => p.StatusId == statusId);
        }

        if (query.OwnerId is { } ownerId)
        {
            projects = projects.Where(p => p.OwnerId == ownerId);
        }

        if (query.MemberId is { } memberId)
        {
            projects = projects.Where(p => p.Members.Any(m => m.UserId == memberId));
        }

        if (query.Archived is { } archived)
        {
            projects = projects.Where(p => p.IsArchived == archived);
        }

        if (query.StartFrom is { } startFrom)
        {
            projects = projects.Where(p => p.StartDate >= startFrom);
        }

        if (query.StartTo is { } startTo)
        {
            projects = projects.Where(p => p.StartDate <= startTo);
        }

        if (query.EndFrom is { } endFrom)
        {
            projects = projects.Where(p => p.EndDate >= endFrom);
        }

        if (query.EndTo is { } endTo)
        {
            projects = projects.Where(p => p.EndDate <= endTo);
        }

        return projects;
    }

    private static IQueryable<Project> Sort(IQueryable<Project> projects, GetProjectsQuery query)
    {
        var descending = query.Descending;
        IOrderedQueryable<Project> ordered = (query.Sort?.ToLowerInvariant()) switch
        {
            "name" => descending ? projects.OrderByDescending(p => p.Name) : projects.OrderBy(p => p.Name),
            "startdate" => descending ? projects.OrderByDescending(p => p.StartDate) : projects.OrderBy(p => p.StartDate),
            "enddate" => descending ? projects.OrderByDescending(p => p.EndDate) : projects.OrderBy(p => p.EndDate),
            _ => descending ? projects.OrderByDescending(p => p.Code) : projects.OrderBy(p => p.Code),
        };

        return ordered.ThenBy(p => p.Id);
    }
}
