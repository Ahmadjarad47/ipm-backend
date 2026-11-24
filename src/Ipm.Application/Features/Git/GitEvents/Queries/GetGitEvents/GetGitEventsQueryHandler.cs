using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Features.Git.GitEvents.Common;
using Ipm.Application.Features.Projects.Projects.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Git;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Git.GitEvents.Queries.GetGitEvents;

internal sealed class GetGitEventsQueryHandler(IApplicationDbContext db, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog)
    : IQueryHandler<GetGitEventsQuery, PagedList<GitEventResponse>>
{
    public async ValueTask<Result<PagedList<GitEventResponse>>> Handle(GetGitEventsQuery query, CancellationToken cancellationToken)
    {
        if (query.ProjectId is not { } projectId)
        {
            return GitErrors.ProjectRequired;
        }

        var guarded = await ProjectGuard.ReadWithAsync(db, currentUser, access, securityLog, projectId, Permissions.GitView, cancellationToken);
        if (guarded.IsFailure)
        {
            return guarded.Error;
        }

        var events = db.GitEvents.AsNoTracking().Where(e => e.ProjectId == projectId);
        if (!string.IsNullOrWhiteSpace(query.WorkItemRef))
        {
            var reference = query.WorkItemRef.Trim().ToUpperInvariant();
            events = events.Where(e => e.WorkItemRefs.Contains(reference));
        }

        if (query.Kind is { } kind)
        {
            var wanted = kind == "push" ? GitEventKind.Push : kind == "merge" ? GitEventKind.Merge : GitEventKind.PullRequest;
            events = events.Where(e => e.Kind == wanted);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            events = events.Where(e => e.Title.Contains(term) || e.Author.Contains(term) || e.Branch.Contains(term));
        }

        var total = await events.CountAsync(cancellationToken);
        var rows = await events.OrderByDescending(e => e.OccurredAt ?? e.CreatedAt).ThenByDescending(e => e.Id).Skip(query.Page.Skip).Take(query.Page.Take).ToListAsync(cancellationToken);
        return new PagedList<GitEventResponse>([.. rows.Select(e => e.ToResponse())], Math.Max(query.Page.Page, 1), query.Page.Take, total);
    }
}
