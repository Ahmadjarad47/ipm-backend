using System.Globalization;
using Ipm.Application.Abstractions.Data;
using Ipm.Domain.Approvals;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Approvals.Approvals.Common;

/// <summary>Loads the subject of an approval and reads its fields as text (snapshots R4, drift R10, rule conditions R2).</summary>
internal static class ApprovalSubjects
{
    /// <summary>The project id of a subject, or null when it does not exist.</summary>
    public static async Task<Guid?> ProjectIdAsync(IApplicationDbContext db, ApprovalSubjectType type, Guid id, CancellationToken cancellationToken)
    {
        if (type == ApprovalSubjectType.Project)
        {
            return await db.Projects.AsNoTracking().Where(p => p.Id == id).Select(p => (Guid?)p.Id).SingleOrDefaultAsync(cancellationToken);
        }

        return await db.WorkItems.AsNoTracking().Where(i => i.Id == id).Select(i => (Guid?)i.ProjectId).SingleOrDefaultAsync(cancellationToken);
    }

    public static async Task<ApprovalSubject?> LoadAsync(IApplicationDbContext db, ApprovalSubjectType type, Guid id, CancellationToken cancellationToken)
    {
        if (await ProjectIdAsync(db, type, id, cancellationToken) is not { } projectId)
        {
            return null;
        }

        var project = await db.Projects.AsNoTracking().Include(p => p.Members).SingleAsync(p => p.Id == projectId, cancellationToken);
        var item = type == ApprovalSubjectType.WorkItem ? await db.WorkItems.AsNoTracking().SingleAsync(i => i.Id == id, cancellationToken) : null;
        return new ApprovalSubject(type, id, project, item);
    }

    /// <summary>The current text value of each field (R4), in the order asked. Ids stay ids, lists are sorted so equal sets compare equal.</summary>
    public static async Task<List<SnapshotEntry>> SnapshotAsync(
        IApplicationDbContext db, ApprovalSubject subject, IEnumerable<string> fields, CancellationToken cancellationToken)
    {
        var entries = new List<SnapshotEntry>();
        foreach (var field in fields)
        {
            entries.Add(new SnapshotEntry(field, await ValueAsync(db, subject, field, cancellationToken)));
        }

        return entries;
    }

    /// <summary>The protected fields whose current value differs from the frozen snapshot (R10).</summary>
    public static async Task<List<string>> ChangedSinceAsync(
        IApplicationDbContext db, ApprovalSubject subject, IEnumerable<SnapshotEntry> snapshot, CancellationToken cancellationToken)
    {
        var changed = new List<string>();
        foreach (var entry in snapshot)
        {
            if (await ValueAsync(db, subject, entry.Field, cancellationToken) != entry.Value)
            {
                changed.Add(entry.Field);
            }
        }

        return changed;
    }

    private static async Task<string?> ValueAsync(IApplicationDbContext db, ApprovalSubject subject, string field, CancellationToken cancellationToken)
    {
        if (subject.Item is { } item)
        {
            return field switch
            {
                "title" => item.Title,
                "description" => item.Description,
                "dueDate" => Text(item.DueDate),
                "startDate" => Text(item.StartDate),
                "parentId" => item.ParentId?.ToString(),
                "priority" => item.Priority.ToString().ToLowerInvariant(),
                "typeId" => item.TypeId.ToString(),
                "assigneeIds" => string.Join(',', item.AssigneeIds.Select(a => a.ToString()).Order(StringComparer.Ordinal)),
                "relations" => await RelationsAsync(db, item.Id, cancellationToken),
                _ => null,
            };
        }

        var project = subject.Project;
        return field switch
        {
            "name" => project.Name,
            "description" => project.Description,
            "startDate" => Text(project.StartDate),
            "endDate" => Text(project.EndDate),
            "ownerId" => project.OwnerId.ToString(),
            "members" => string.Join(',', project.Members.Select(m => $"{m.UserId}:{m.RoleId}").Order(StringComparer.Ordinal)),
            _ => null,
        };
    }

    private static async Task<string> RelationsAsync(IApplicationDbContext db, Guid itemId, CancellationToken cancellationToken)
    {
        var rows = await db.WorkItemRelations.AsNoTracking()
            .Where(r => r.SourceId == itemId || r.TargetId == itemId)
            .Select(r => new { r.RelationTypeId, r.SourceId, r.TargetId })
            .ToListAsync(cancellationToken);
        return string.Join(',', rows.Select(r => $"{r.RelationTypeId}:{r.SourceId}>{r.TargetId}").Order(StringComparer.Ordinal));
    }

    /// <summary>The value a rule condition reads from the subject (R2).</summary>
    public static string? ConditionValue(ApprovalSubject subject, string field)
    {
        if (subject.Item is { } item)
        {
            return field switch
            {
                "title" => item.Title,
                "description" => item.Description,
                "dueDate" => Text(item.DueDate),
                "startDate" => Text(item.StartDate),
                "parentId" => item.ParentId?.ToString(),
                "priority" => item.Priority.ToString().ToLowerInvariant(),
                "typeId" => item.TypeId.ToString(),
                "statusId" => item.StatusId.ToString(),
                "ref" => item.Ref,
                _ => null,
            };
        }

        var project = subject.Project;
        return field switch
        {
            "name" => project.Name,
            "code" => project.Code,
            "startDate" => Text(project.StartDate),
            "endDate" => Text(project.EndDate),
            "ownerId" => project.OwnerId.ToString(),
            "statusId" => project.StatusId.ToString(),
            _ => null,
        };
    }

    private static string? Text(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
