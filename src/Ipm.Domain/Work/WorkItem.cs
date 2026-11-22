using System.Text.Json;
using Ipm.Domain.Common;
using Ipm.Domain.Config;

namespace Ipm.Domain.Work;

/// <summary>
/// A unit of work inside a project (feature, task, bug…). The <see cref="Ref"/> (<c>ERP-104</c>) never changes. Assignees and
/// custom field values are columns of the item itself, so changing them moves its version (ETag) and shows in audit_log (R2).
/// The status moves only through a transition (R7), the type only through a type change (R10), and the parent only through
/// <see cref="SetParent"/> (R6). Never deleted, only archived (R3).
/// </summary>
public sealed class WorkItem : AuditableEntity
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 20000;

    private WorkItem() { }

    private WorkItem(Guid id) : base(id) { }

    public Guid ProjectId { get; private set; }

    public int Number { get; private set; }

    public string Ref { get; private set; } = string.Empty;

    public Guid TypeId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public Guid StatusId { get; private set; }

    public Priority Priority { get; private set; }

    public DateOnly? StartDate { get; private set; }

    public DateOnly? DueDate { get; private set; }

    public Guid? ParentId { get; private set; }

    public List<Guid> AssigneeIds { get; private set; } = [];

    public Dictionary<string, JsonElement> Fields { get; private set; } = [];

    public bool IsArchived { get; private set; }

    /// <summary>The ref of an item: the project code, a dash, and the number.</summary>
    public static string MakeRef(string projectCode, int number) => $"{projectCode}-{number}";

    /// <summary>R1. <paramref name="id"/> is only given by the seed, to keep it idempotent. Field values are checked by <see cref="WorkItemFieldValues"/>.</summary>
    public static Result<WorkItem> Create(
        Guid projectId, string projectCode, int number, Guid typeId, Guid statusId, string title, string description, Priority priority,
        DateOnly? startDate, DateOnly? dueDate, Guid? parentId, IEnumerable<Guid> assigneeIds, IReadOnlyDictionary<string, JsonElement>? fields, Guid? id = null)
    {
        var item = id is { } fixedId ? new WorkItem(fixedId) : new WorkItem();
        item.ProjectId = projectId;
        item.Number = number;
        item.Ref = MakeRef(projectCode, number);
        item.TypeId = typeId;
        item.StatusId = statusId;
        item.ParentId = parentId;

        var updated = item.Update(title, description, priority, startDate, dueDate, assigneeIds, fields);
        return updated.IsFailure ? updated.Error : item;
    }

    /// <summary>R2: the plain fields. The due date is not before the start date.</summary>
    public Result Update(
        string title, string description, Priority priority, DateOnly? startDate, DateOnly? dueDate, IEnumerable<Guid> assigneeIds,
        IReadOnlyDictionary<string, JsonElement>? fields)
    {
        if (startDate is { } start && dueDate is { } due && due < start)
        {
            return WorkItemErrors.InvalidDates;
        }

        Title = title.Trim();
        Description = description.Trim();
        Priority = priority;
        StartDate = startDate;
        DueDate = dueDate;
        AssigneeIds = [.. assigneeIds.Distinct()];
        Fields = WorkItemFieldValues.Normalize(fields);
        return Result.Success();
    }

    /// <summary>R12: the bulk actions change one thing at a time.</summary>
    public void SetAssignees(IEnumerable<Guid> assigneeIds) => AssigneeIds = [.. assigneeIds.Distinct()];

    /// <summary>R12.</summary>
    public void SetPriority(Priority priority) => Priority = priority;

    /// <summary>R12: the due date is not before the start date.</summary>
    public Result SetDueDate(DateOnly? dueDate)
    {
        if (StartDate is { } start && dueDate is { } due && due < start)
        {
            return WorkItemErrors.InvalidDates;
        }

        DueDate = dueDate;
        return Result.Success();
    }

    /// <summary>R2: a read-only (archived) item is refused.</summary>
    public Result EnsureEditable() => IsArchived ? WorkItemErrors.Archived : Result.Success();

    /// <summary>R3: the handler checks that no active children are left.</summary>
    public void Archive() => IsArchived = true;

    /// <summary>R7: the only way the status changes. The workflow rules are checked by the handler.</summary>
    public void MoveTo(Guid statusId) => StatusId = statusId;

    /// <summary>R6: the handler checks the parent (same project, allowed type, no cycle).</summary>
    public void SetParent(Guid? parentId) => ParentId = parentId;

    /// <summary>R10: the handler checks the conflicts first. The status may change when the new type workflow starts elsewhere.</summary>
    public void ChangeType(Guid typeId, IReadOnlyDictionary<string, JsonElement> fields)
    {
        TypeId = typeId;
        Fields = WorkItemFieldValues.Normalize(fields);
    }
}
