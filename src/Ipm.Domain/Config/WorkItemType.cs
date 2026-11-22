using System.Text.RegularExpressions;
using Ipm.Domain.Common;

namespace Ipm.Domain.Config;

/// <summary>
/// A kind of work item (feature, task, bug…) with its custom fields and the types allowed as its children (FR-TYP, FR-REL-01).
/// Types are never deleted, only deactivated (R4), and the <see cref="Key"/> never changes (R1). Fields and children are
/// columns of the type itself, so changing them changes its version (ETag) and shows in audit_log.
/// </summary>
public sealed partial class WorkItemType : AuditableEntity
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 300;
    public const int KeyMaxLength = 31;

    private WorkItemType() { }

    private WorkItemType(Guid id) : base(id) { }

    public string Key { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    /// <summary>Display color, <c>#RRGGBB</c>.</summary>
    public string Color { get; private set; } = string.Empty;

    public WorkItemIcon Icon { get; private set; }

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; } = true;

    /// <summary>The work item workflow of this type (R13). Null only for a row that predates part 03; the seeder binds it.</summary>
    public Guid? WorkflowId { get; private set; }

    public List<FieldDefinition> Fields { get; private set; } = [];

    /// <summary>Types allowed as direct children (may include this type itself).</summary>
    public List<Guid> AllowedChildTypeIds { get; private set; } = [];

    /// <summary>Keys are compared and stored in lower case, so <c>Bug</c> and <c>bug</c> are the same key.</summary>
    public static string NormalizeKey(string key) => key.Trim().ToLowerInvariant();

    public static bool IsValidKey(string normalizedKey) => KeyPattern().IsMatch(normalizedKey);

    public static bool IsValidColor(string color) => ColorPattern().IsMatch(color);

    /// <summary><paramref name="id"/> is only given by the seed, to keep it idempotent.</summary>
    public static Result<WorkItemType> Create(
        string key, string name, string description, string color, WorkItemIcon icon, int sortOrder, bool isActive, Guid? id = null)
    {
        var normalized = NormalizeKey(key);
        if (!IsValidKey(normalized))
        {
            return WorkItemTypeErrors.KeyInvalid;
        }

        var type = id is { } fixedId ? new WorkItemType(fixedId) : new WorkItemType();
        type.Key = normalized;
        type.Update(name, description, color, icon, sortOrder, isActive);
        return type;
    }

    public void Update(string name, string description, string color, WorkItemIcon icon, int sortOrder, bool isActive)
    {
        Name = name.Trim();
        Description = description.Trim();
        Color = color.Trim();
        Icon = icon;
        SortOrder = sortOrder;
        IsActive = isActive;
    }

    /// <summary>R1: a request may repeat the key, but never a different one.</summary>
    public Result EnsureKeyUnchanged(string? requestedKey) =>
        requestedKey is null || NormalizeKey(requestedKey) == Key ? Result.Success() : WorkItemTypeErrors.KeyImmutable;

    /// <summary>R3: replaces the whole list of custom fields, ordered by their display order.</summary>
    public Result SetFields(IEnumerable<FieldDefinition> fields)
    {
        var wanted = fields.ToList();
        if (FieldDefinitions.Validate(wanted).Count > 0)
        {
            return WorkItemTypeErrors.InvalidFields;
        }

        // A new list, so EF Core always sees the change.
        Fields = [.. wanted.OrderBy(f => f.Order)];
        return Result.Success();
    }

    /// <summary>R13: the handler checks that the workflow exists and is a work item workflow.</summary>
    public void SetWorkflow(Guid workflowId) => WorkflowId = workflowId;

    /// <summary>R5 (the types exist) is checked by the handler. Duplicates are dropped, the order of the request is kept.</summary>
    public void SetAllowedChildren(IEnumerable<Guid> childTypeIds) => AllowedChildTypeIds = [.. childTypeIds.Distinct()];

    [GeneratedRegex("^[a-z][a-z0-9_]{1,30}$")]
    private static partial Regex KeyPattern();

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex ColorPattern();
}
