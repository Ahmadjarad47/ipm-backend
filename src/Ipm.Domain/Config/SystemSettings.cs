using Ipm.Domain.Common;

namespace Ipm.Domain.Config;

/// <summary>Spec 09: the one record of organization-wide policies. Audited like any entity, so every change keeps its before and after.</summary>
public sealed class SystemSettings : AuditableEntity
{
    public static readonly Guid SingletonId = new("01970000-0000-7000-8000-000000000001");

    private SystemSettings() : base(SingletonId) { }

    public int SessionHours { get; private set; }

    public int CommentEditHours { get; private set; }

    public int AttachmentMaxMb { get; private set; }

    public List<string> AttachmentExtensions { get; private set; } = [];

    public bool AllowCrossProjectRelations { get; private set; }

    public bool AllowSelfApproval { get; private set; }

    public string TimeZone { get; private set; } = string.Empty;

    public int AuditRetentionDays { get; private set; }

    public bool ProjectsPrivateByDefault { get; private set; }

    public List<Guid> ProgressIncludedTypeIds { get; private set; } = [];

    public static SystemSettings CreateDefault()
    {
        var settings = new SystemSettings();
        settings.Apply(SettingsValues.Defaults);
        return settings;
    }

    public SettingsValues Values() => new(
        SessionHours, CommentEditHours, AttachmentMaxMb, [.. AttachmentExtensions], AllowCrossProjectRelations, AllowSelfApproval, TimeZone,
        AuditRetentionDays, ProjectsPrivateByDefault, [.. ProgressIncludedTypeIds]);

    /// <summary>The values were checked by the handler (R3).</summary>
    public void Apply(SettingsValues values)
    {
        SessionHours = values.SessionHours;
        CommentEditHours = values.CommentEditHours;
        AttachmentMaxMb = values.AttachmentMaxMb;
        AttachmentExtensions = [.. values.AttachmentExtensions];
        AllowCrossProjectRelations = values.AllowCrossProjectRelations;
        AllowSelfApproval = values.AllowSelfApproval;
        TimeZone = values.TimeZone;
        AuditRetentionDays = values.AuditRetentionDays;
        ProjectsPrivateByDefault = values.ProjectsPrivateByDefault;
        ProgressIncludedTypeIds = [.. values.ProgressIncludedTypeIds];
    }
}
