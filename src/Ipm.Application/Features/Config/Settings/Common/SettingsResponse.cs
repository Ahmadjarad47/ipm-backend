namespace Ipm.Application.Features.Config.Settings.Common;

/// <summary>The organization settings. <see cref="Version"/> is sent as the ETag.</summary>
public sealed record SettingsResponse(
    int SessionHours,
    int CommentEditHours,
    int AttachmentMaxMb,
    IReadOnlyList<string> AttachmentExtensions,
    bool AllowCrossProjectRelations,
    bool AllowSelfApproval,
    string TimeZone,
    int AuditRetentionDays,
    bool ProjectsPrivateByDefault,
    IReadOnlyList<Guid> ProgressIncludedTypeIds,
    uint Version);
