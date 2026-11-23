namespace Ipm.Domain.Config;

/// <summary>Spec 09 §2: the organization policies as plain values (what the cache holds and what the rest of the system reads).</summary>
public sealed record SettingsValues(
    int SessionHours,
    int CommentEditHours,
    int AttachmentMaxMb,
    IReadOnlyList<string> AttachmentExtensions,
    bool AllowCrossProjectRelations,
    bool AllowSelfApproval,
    string TimeZone,
    int AuditRetentionDays,
    bool ProjectsPrivateByDefault,
    IReadOnlyList<Guid> ProgressIncludedTypeIds)
{
    /// <summary>Executable and script types are never allowed, whatever the settings say.</summary>
    public static readonly IReadOnlyList<string> BlockedExtensions = ["exe", "bat", "cmd", "sh", "ps1", "msi", "js", "vbs", "jar", "scr", "dll", "com"];

    /// <summary>The values before anyone changes them: the constants the earlier parts used.</summary>
    public static SettingsValues Defaults { get; } = new(
        SessionHours: 8,
        CommentEditHours: 24,
        AttachmentMaxMb: 10,
        AttachmentExtensions: ["pdf", "png", "jpg", "jpeg", "gif", "txt", "csv", "docx", "xlsx", "pptx", "zip"],
        AllowCrossProjectRelations: false,
        AllowSelfApproval: false,
        TimeZone: "Asia/Riyadh",
        AuditRetentionDays: 2555,
        ProjectsPrivateByDefault: true,
        ProgressIncludedTypeIds: []);
}
