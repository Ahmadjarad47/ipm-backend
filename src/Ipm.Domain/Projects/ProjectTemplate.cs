using Ipm.Domain.Common;

namespace Ipm.Domain.Projects;

/// <summary>
/// Presets applied once when a project is created (FR-PRJ-10): allowed and default types, suggested roles and members, and the
/// default duration. Changing a template never touches projects already made from it. Never deleted, only deactivated.
/// </summary>
public sealed class ProjectTemplate : AuditableEntity
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 300;

    private ProjectTemplate() { }

    private ProjectTemplate(Guid id) : base(id) { }

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public bool IsActive { get; private set; } = true;

    public List<Guid> AllowedTypeIds { get; private set; } = [];

    public Guid DefaultTypeId { get; private set; }

    /// <summary>The initial status of the project workflow (R14).</summary>
    public Guid InitialStatusId { get; private set; }

    public List<Guid> SuggestedRoleIds { get; private set; } = [];

    public int DurationDays { get; private set; }

    public List<SuggestedMember> SuggestedMembers { get; private set; } = [];

    /// <summary><paramref name="id"/> is only given by the seed, to keep it idempotent.</summary>
    public static Result<ProjectTemplate> Create(
        string name, string description, bool isActive, IEnumerable<Guid> allowedTypeIds, Guid defaultTypeId, Guid initialStatusId,
        IEnumerable<Guid> suggestedRoleIds, int durationDays, IEnumerable<SuggestedMember> suggestedMembers, Guid? id = null)
    {
        var template = id is { } fixedId ? new ProjectTemplate(fixedId) : new ProjectTemplate();
        var result = template.Update(name, description, isActive, allowedTypeIds, defaultTypeId, initialStatusId, suggestedRoleIds, durationDays, suggestedMembers);
        if (result.IsFailure)
        {
            return result.Error;
        }

        return template;
    }

    /// <summary>R14 (structure): at least one type, the default is one of them, the duration is at least a day. The template is unchanged on failure.</summary>
    public Result Update(
        string name, string description, bool isActive, IEnumerable<Guid> allowedTypeIds, Guid defaultTypeId, Guid initialStatusId,
        IEnumerable<Guid> suggestedRoleIds, int durationDays, IEnumerable<SuggestedMember> suggestedMembers)
    {
        var allowed = allowedTypeIds.Distinct().ToList();
        if (allowed.Count == 0 || !allowed.Contains(defaultTypeId))
        {
            return ProjectTemplateErrors.InvalidTypes;
        }

        if (durationDays < 1)
        {
            return ProjectTemplateErrors.InvalidDuration;
        }

        Name = name.Trim();
        Description = description.Trim();
        IsActive = isActive;
        AllowedTypeIds = allowed;
        DefaultTypeId = defaultTypeId;
        InitialStatusId = initialStatusId;
        SuggestedRoleIds = [.. suggestedRoleIds.Distinct()];
        DurationDays = durationDays;
        SuggestedMembers = [.. suggestedMembers.Distinct()];
        return Result.Success();
    }
}
