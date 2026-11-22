using Ipm.Application.Abstractions.Data;
using Ipm.Domain.Config;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Projects.Projects.Common;

/// <summary>R13 of part 05: progress, item count and overdue count of projects, from their work items.</summary>
internal static class ProjectWorkStats
{
    public const string Rule = "leaf-items-done-over-counted";

    public static async Task<Dictionary<Guid, (ProjectProgress Progress, int Count, int Overdue)>> LoadAsync(
        IApplicationDbContext db, IReadOnlyCollection<Guid> projectIds, DateOnly today, CancellationToken cancellationToken)
    {
        var rows = await (
                from i in db.WorkItems.AsNoTracking()
                where projectIds.Contains(i.ProjectId) && !i.IsArchived
                join s in db.Statuses on i.StatusId equals s.Id
                select new
                {
                    i.ProjectId,
                    i.DueDate,
                    s.Category,
                    IsLeaf = !db.WorkItems.Any(c => c.ParentId == i.Id && !c.IsArchived),
                })
            .ToListAsync(cancellationToken);

        var result = new Dictionary<Guid, (ProjectProgress, int, int)>();
        foreach (var id in projectIds)
        {
            var own = rows.Where(r => r.ProjectId == id).ToList();
            var counted = own.Count(r => r.IsLeaf && r.Category != StatusCategory.Cancelled);
            var done = own.Count(r => r.IsLeaf && r.Category == StatusCategory.Done);
            var overdue = own.Count(r => r.DueDate < today && r.Category is not (StatusCategory.Done or StatusCategory.Cancelled));
            int? percent = counted == 0 ? null : (int)Math.Round(done * 100.0 / counted, MidpointRounding.AwayFromZero);
            result[id] = (new ProjectProgress(percent, done, counted, Rule), own.Count, overdue);
        }

        return result;
    }

    public static ProjectResponse With(this ProjectResponse response, Dictionary<Guid, (ProjectProgress Progress, int Count, int Overdue)> stats) =>
        stats.TryGetValue(response.Id, out var s) ? response with { Progress = s.Progress, WorkItemCount = s.Count, OverdueCount = s.Overdue } : response;
}
