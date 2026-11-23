using System.Text.Json;
using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common;
using Ipm.Application.Features.Activity.Common;
using Ipm.Application.Features.Approvals.Approvals.Common;
using Ipm.Application.Features.Dashboard.Stats.Common;
using Ipm.Application.Features.Identity.Common;
using Ipm.Domain.Approvals;
using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Dashboard.Stats.Queries.GetDashboardStats;

internal sealed class GetDashboardStatsQueryHandler(IApplicationDbContext db, IUserContext currentUser, IAccessService access, TimeProvider time)
    : IQueryHandler<GetDashboardStatsQuery, DashboardStatsResponse>
{
    private const string BlocksKey = "blocks";

    private sealed record Row(Guid Id, Guid? ParentId, Guid TypeId, Guid StatusId, DateOnly? StartDate, DateOnly? DueDate, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt, List<Guid> AssigneeIds);

    /// <summary>A comma list of kebab categories; null when one of them is unknown.</summary>
    public static List<StatusCategory>? ParseCategories(string text)
    {
        var result = new List<StatusCategory>();
        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var found = Enum.GetValues<StatusCategory>().Cast<StatusCategory?>().FirstOrDefault(c => string.Equals(Key(c!.Value), part, StringComparison.OrdinalIgnoreCase));
            if (found is null)
            {
                return null;
            }

            result.Add(found.Value);
        }

        return result;
    }

    public static string Key(StatusCategory category) => JsonNamingPolicy.KebabCaseLower.ConvertName(category.ToString());

    public async ValueTask<Result<DashboardStatsResponse>> Handle(GetDashboardStatsQuery query, CancellationToken cancellationToken)
    {
        var caller = await AccessChecks.CallerAsync(currentUser, access, cancellationToken);
        if (caller.IsFailure)
        {
            return caller.Error;
        }

        var access_ = caller.Value;
        var categories = string.IsNullOrWhiteSpace(query.StatusCategory) ? [] : ParseCategories(query.StatusCategory) ?? [];
        var today = RiyadhClock.Today(time);

        // R8: the projects the caller can see, with the dashboard permission, not archived; a project filter only narrows them.
        var projects = await db.Projects.AsNoTracking().Where(p => !p.IsArchived).Select(p => new { p.Id, p.StatusId }).ToListAsync(cancellationToken);
        projects = [.. projects.Where(p => access_.Can(Permissions.ProjectsView, p.Id) && access_.Can(Permissions.DashboardView, p.Id) && (query.ProjectId is null || p.Id == query.ProjectId))];
        var projectIds = projects.Select(p => p.Id).ToList();

        var statuses = await db.Statuses.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Category, cancellationToken);
        StatusCategory CategoryOf(Guid statusId) => statuses.GetValueOrDefault(statusId, StatusCategory.NotStarted);
        bool IsClosed(Guid statusId) => CategoryOf(statusId) is StatusCategory.Done or StatusCategory.Cancelled;

        var all = await db.WorkItems.AsNoTracking()
            .Where(i => !i.IsArchived && projectIds.Contains(i.ProjectId))
            .Select(i => new Row(i.Id, i.ParentId, i.TypeId, i.StatusId, i.StartDate, i.DueDate, i.CreatedAt, i.UpdatedAt, i.AssigneeIds))
            .ToListAsync(cancellationToken);

        // R9: an item is in the period when its span overlaps it.
        bool Overlaps(Row r)
        {
            var start = r.StartDate ?? RiyadhClock.DayOf(r.CreatedAt);
            var end = r.DueDate ?? RiyadhClock.DayOf(r.UpdatedAt ?? r.CreatedAt);
            var (lo, hi) = start <= end ? (start, end) : (end, start);
            return (query.From is null || hi >= query.From) && (query.To is null || lo <= query.To);
        }

        var items = all
            .Where(r => Overlaps(r) && (categories.Count == 0 || categories.Contains(CategoryOf(r.StatusId))) && (query.AssigneeId is null || r.AssigneeIds.Contains(query.AssigneeId.Value)))
            .ToList();

        // R10: parents are counted in byCategory, leaves only in leafByCategory, byType and progress.
        var parents = all.Where(r => r.ParentId is not null).Select(r => r.ParentId!.Value).ToHashSet();
        Dictionary<string, int> Empty() => Enum.GetValues<StatusCategory>().ToDictionary(Key, _ => 0);
        var byCategory = Empty();
        var leafByCategory = Empty();
        foreach (var item in items)
        {
            byCategory[Key(CategoryOf(item.StatusId))]++;
            if (!parents.Contains(item.Id))
            {
                leafByCategory[Key(CategoryOf(item.StatusId))]++;
            }
        }

        var types = await db.WorkItemTypes.AsNoTracking().OrderBy(t => t.SortOrder).Select(t => t.Id).ToListAsync(cancellationToken);
        var byType = new List<DashboardTypeStats>();
        foreach (var typeId in types)
        {
            var leaves = items.Where(r => r.TypeId == typeId && !parents.Contains(r.Id) && CategoryOf(r.StatusId) != StatusCategory.Cancelled).ToList();
            if (leaves.Count == 0)
            {
                continue;
            }

            var per = Empty();
            leaves.ForEach(r => per[Key(CategoryOf(r.StatusId))]++);
            byType.Add(new DashboardTypeStats(typeId, leaves.Count, per[Key(StatusCategory.Done)], per));
        }

        // A blocked item is the target of a "blocks" link whose source is still open, while it is open itself.
        var itemIds = items.Select(r => r.Id).ToList();
        var links = await (
                from r in db.WorkItemRelations.AsNoTracking()
                join t in db.RelationTypes.AsNoTracking() on r.RelationTypeId equals t.Id
                where t.Key == BlocksKey && itemIds.Contains(r.TargetId)
                select new { r.SourceId, r.TargetId })
            .ToListAsync(cancellationToken);
        var sourceStatus = await db.WorkItems.AsNoTracking()
            .Where(i => links.Select(l => l.SourceId).Contains(i.Id))
            .Select(i => new { i.Id, i.StatusId })
            .ToDictionaryAsync(i => i.Id, i => i.StatusId, cancellationToken);
        var itemStatus = items.ToDictionary(r => r.Id, r => r.StatusId);
        var open = links.Where(l => sourceStatus.TryGetValue(l.SourceId, out var s) && !IsClosed(s) && !IsClosed(itemStatus[l.TargetId])).ToList();
        var blocked = open.Select(l => l.TargetId).Distinct().Count();
        var blocking = open.Select(l => l.SourceId).Distinct().Count();
        var openItems = items.Where(r => !IsClosed(r.StatusId)).ToList();

        var activeStatuses = statuses.Where(s => s.Value == StatusCategory.Active).Select(s => s.Key).ToHashSet();

        var pending = await db.ApprovalRequests.AsNoTracking().Where(r => r.Status == ApprovalStatus.PendingApproval && r.RequesterId != access_.UserId).ToListAsync(cancellationToken);
        var deciders = await ApprovalDeciders.LoadAsync(db, today, cancellationToken);

        var gitProjects = projectIds.Where(id => access_.Can(Permissions.GitView, id)).ToList();
        var gitFrom = AuditAccess.StartOf(query.From);
        var gitTo = AuditAccess.EndOf(query.To);
        var git = await db.GitEvents.AsNoTracking()
            .CountAsync(g => gitProjects.Contains(g.ProjectId) && (gitFrom == null || g.CreatedAt >= gitFrom) && (gitTo == null || g.CreatedAt < gitTo), cancellationToken);

        return new DashboardStatsResponse(
            projects.Count(p => activeProjectStatuses(p.StatusId)),
            projects.Count,
            openItems.Count,
            openItems.Count(r => r.DueDate < today),
            pending.Count(r => deciders.IsApprover(r, access_.UserId)),
            byCategory,
            leafByCategory,
            byType,
            blocked,
            new DashboardDependencies(blocked, blocking, openItems.Count == 0 ? 0 : (int)Math.Round(blocked * 100.0 / openItems.Count, MidpointRounding.AwayFromZero)),
            git);

        bool activeProjectStatuses(Guid statusId) => activeStatuses.Contains(statusId);
    }
}
