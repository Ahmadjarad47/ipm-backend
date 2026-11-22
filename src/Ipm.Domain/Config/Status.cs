using System.Text.RegularExpressions;
using Ipm.Domain.Common;

namespace Ipm.Domain.Config;

/// <summary>
/// A state a project or work item can be in (جديد، قيد التنفيذ، مكتمل…). The <see cref="Key"/> and <see cref="Scope"/> never
/// change (history and integrations point at them, R1 R2); the name, color and description change freely.
/// Statuses are never deleted, only deactivated (R4).
/// </summary>
public sealed partial class Status : AuditableEntity
{
    public const int KeyMaxLength = 31;
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 300;

    private Status() { }

    private Status(Guid id) : base(id) { }

    public string Key { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    /// <summary>Display color, <c>#RRGGBB</c>.</summary>
    public string Color { get; private set; } = string.Empty;

    public StatusCategory Category { get; private set; }

    public StatusScope Scope { get; private set; }

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; } = true;

    /// <summary>Keys are compared and stored in lower case, so <c>Done</c> and <c>done</c> are the same key.</summary>
    public static string NormalizeKey(string key) => key.Trim().ToLowerInvariant();

    public static bool IsValidKey(string normalizedKey) => KeyPattern().IsMatch(normalizedKey);

    public static bool IsValidColor(string color) => ColorPattern().IsMatch(color);

    /// <summary><paramref name="id"/> is only given by the seed, to keep it idempotent.</summary>
    public static Result<Status> Create(
        string key, string name, string description, string color, StatusCategory category, StatusScope scope, int sortOrder, bool isActive, Guid? id = null)
    {
        var normalized = NormalizeKey(key);
        if (!IsValidKey(normalized))
        {
            return StatusErrors.KeyInvalid;
        }

        var status = id is { } fixedId ? new Status(fixedId) : new Status();
        status.Key = normalized;
        status.Scope = scope;
        status.Update(name, description, color, category, sortOrder, isActive);
        return status;
    }

    public void Update(string name, string description, string color, StatusCategory category, int sortOrder, bool isActive)
    {
        Name = name.Trim();
        Description = description.Trim();
        Color = color.Trim();
        Category = category;
        SortOrder = sortOrder;
        IsActive = isActive;
    }

    /// <summary>R1: a request may repeat the key, but never a different one.</summary>
    public Result EnsureKeyUnchanged(string? requestedKey) =>
        requestedKey is null || NormalizeKey(requestedKey) == Key ? Result.Success() : StatusErrors.KeyImmutable;

    /// <summary>R2: a request may repeat the scope, but never a different one.</summary>
    public Result EnsureScopeUnchanged(StatusScope? requestedScope) =>
        requestedScope is null || requestedScope == Scope ? Result.Success() : StatusErrors.ScopeImmutable;

    [GeneratedRegex("^[a-z][a-z0-9_]{1,30}$")]
    private static partial Regex KeyPattern();

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex ColorPattern();
}
