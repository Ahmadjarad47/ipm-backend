using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Abstractions.Reports;
using Ipm.Application.Common;
using Ipm.Application.Features.Identity.Common;
using Ipm.Application.Features.Reports.Work.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Ipm.Domain.Identity;
using Ipm.Domain.Work;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Reports.Work.Queries.GetWorkReport;

/// <summary>Spec 10 R1 to R5.</summary>
internal sealed class GetWorkReportQueryHandler(IApplicationDbContext db, IUserContext currentUser, IAccessService access, IWorkReportWriter writer, TimeProvider time)
    : IQueryHandler<GetWorkReportQuery, WorkReportFile>
{
    public const int MaxRows = 20_000;

    private static readonly Dictionary<Priority, string> PriorityNames = new()
    {
        [Priority.Low] = "منخفضة",
        [Priority.Medium] = "متوسطة",
        [Priority.High] = "عالية",
        [Priority.Critical] = "حرجة",
    };

    private sealed record Line(Guid ProjectId, WorkReportRow Row, bool Created, bool Completed);

    public async ValueTask<Result<WorkReportFile>> Handle(GetWorkReportQuery query, CancellationToken cancellationToken)
    {
        var caller = await AccessChecks.CallerAsync(currentUser, access, cancellationToken);
        if (caller.IsFailure)
        {
            return caller.Error;
        }

        var who = caller.Value;
        var today = RiyadhClock.Today(time);

        // R1: the projects the caller may see and export; a project filter only narrows them, and one they may not see is simply absent.
        var projects = await db.Projects.AsNoTracking().Select(p => new { p.Id, p.Code, p.Name }).OrderBy(p => p.Code).ToListAsync(cancellationToken);
        projects = [.. projects.Where(p => who.Can(Permissions.ProjectsView, p.Id) && who.Can(Permissions.ReportsExport, p.Id) && (query.ProjectIds.Count == 0 || query.ProjectIds.Contains(p.Id)))];
        var projectIds = projects.Select(p => p.Id).ToList();
        var projectNames = projects.ToDictionary(p => p.Id, p => p.Name);

        string? personName = null;
        if (query.AssigneeId is { } assigneeId)
        {
            personName = await db.Users.AsNoTracking().Where(u => u.Id == assigneeId).Select(u => u.Name).SingleOrDefaultAsync(cancellationToken);
            if (personName is null)
            {
                return Error.NotFound("Report.PersonNotFound", "الشخص المطلوب غير موجود.");
            }
        }

        var statuses = await db.Statuses.AsNoTracking().ToDictionaryAsync(s => s.Id, s => new { s.Name, s.Category }, cancellationToken);
        var types = await db.WorkItemTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);
        var people = await db.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);

        var all = await db.WorkItems.AsNoTracking()
            .Where(i => !i.IsArchived && projectIds.Contains(i.ProjectId) && (query.AssigneeId == null || i.AssigneeIds.Contains(query.AssigneeId.Value)))
            .ToListAsync(cancellationToken);
        var itemIds = all.Select(i => i.Id).ToList();
        var changes = await db.WorkItemStatusChanges.AsNoTracking()
            .Where(c => itemIds.Contains(c.WorkItemId))
            .Select(c => new { c.WorkItemId, c.ToStatusId, c.At })
            .ToListAsync(cancellationToken);

        bool InPeriod(DateTimeOffset at)
        {
            var day = RiyadhClock.DayOf(at);
            return day >= query.From && day <= query.To;
        }

        bool IsClosed(Guid statusId) => statuses.GetValueOrDefault(statusId)?.Category is StatusCategory.Done or StatusCategory.Cancelled;

        // R2: an item is in the period when it was created in it, changed status in it, or was updated in it.
        var lines = new List<Line>();
        foreach (var item in all.OrderBy(i => i.Ref, StringComparer.Ordinal))
        {
            var created = InPeriod(item.CreatedAt);
            var inPeriod = changes.Where(c => c.WorkItemId == item.Id && InPeriod(c.At)).ToList();
            var completed = inPeriod.Any(c => statuses.GetValueOrDefault(c.ToStatusId)?.Category == StatusCategory.Done);
            var updated = item.UpdatedAt is { } at && InPeriod(at);
            if (!created && inPeriod.Count == 0 && !updated)
            {
                continue;
            }

            var reasons = new List<string>();
            if (created)
            {
                reasons.Add("جديد");
            }

            if (completed)
            {
                reasons.Add("أُنجز");
            }
            else if (inPeriod.Count > 0)
            {
                reasons.Add("تغيّرت حالته");
            }

            if (reasons.Count == 0)
            {
                reasons.Add("تحديث");
            }

            lines.Add(new Line(
                item.ProjectId,
                new WorkReportRow(
                    item.Ref,
                    projectNames[item.ProjectId],
                    item.Title,
                    types.GetValueOrDefault(item.TypeId, "—"),
                    statuses.GetValueOrDefault(item.StatusId)?.Name ?? "—",
                    PriorityNames.GetValueOrDefault(item.Priority, item.Priority.ToString()),
                    string.Join("، ", item.AssigneeIds.Select(a => people.GetValueOrDefault(a, "—"))),
                    item.StartDate,
                    item.DueDate,
                    RiyadhClock.DayOf(item.CreatedAt),
                    RiyadhClock.DayOf(item.UpdatedAt ?? item.CreatedAt),
                    !IsClosed(item.StatusId) && item.DueDate is { } due && due < today,
                    string.Join("، ", reasons)),
                created,
                completed));
        }

        if (lines.Count > MaxRows)
        {
            return Error.Failure("Report.TooLarge", $"النتيجة {lines.Count} من الأعمال وتتجاوز الحد ({MaxRows}). ضيّق الفترة أو اختر مشاريع أقل.");
        }

        var summaries = projects
            .Select(p =>
            {
                var mine = lines.Where(l => l.ProjectId == p.Id).ToList();
                var open = all.Where(i => i.ProjectId == p.Id && !IsClosed(i.StatusId)).ToList();
                return new WorkReportProjectSummary(
                    p.Code, p.Name, mine.Count, mine.Count(l => l.Created), mine.Count(l => l.Completed), open.Count, open.Count(i => i.DueDate is { } d && d < today));
            })
            .Where(s => query.ProjectIds.Count > 0 || s.Touched > 0 || s.OpenNow > 0)
            .ToList();

        var report = new WorkReportData(query.From, query.To, today, personName, summaries, [.. lines.Select(l => l.Row)]);
        return new WorkReportFile($"report-{query.From:yyyyMMdd}-{query.To:yyyyMMdd}.xlsx", writer.Write(report));
    }
}
