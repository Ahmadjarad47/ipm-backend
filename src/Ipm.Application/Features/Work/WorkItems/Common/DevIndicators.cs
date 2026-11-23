using Ipm.Application.Abstractions.Data;
using Ipm.Domain.Git;
using Ipm.Domain.Work;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Work.WorkItems.Common;

internal static class DevIndicators
{
    /// <summary>The indicator of each item, keyed by item id, counted from the Git events of its project that mention its ref.</summary>
    public static async Task<Dictionary<Guid, DevIndicator>> LoadAsync(IApplicationDbContext db, IReadOnlyCollection<WorkItem> items, CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, DevIndicator>();
        if (items.Count == 0)
        {
            return result;
        }

        var projectIds = items.Select(i => i.ProjectId).Distinct().ToList();
        var events = await db.GitEvents.AsNoTracking()
            .Where(e => projectIds.Contains(e.ProjectId) && e.WorkItemRefs.Count > 0)
            .Select(e => new { e.ProjectId, e.Kind, e.PrState, e.WorkItemRefs })
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            var own = events.Where(e => e.ProjectId == item.ProjectId && e.WorkItemRefs.Contains(item.Ref)).ToList();
            result[item.Id] = new DevIndicator(
                own.Count(e => e.Kind == GitEventKind.Push),
                own.Count(e => e.Kind == GitEventKind.PullRequest && e.PrState == PullRequestState.Open),
                own.Count(e => e.PrState == PullRequestState.Merged));
        }

        return result;
    }
}
