using Ipm.Domain.Config;

namespace Ipm.Application.Features.Config.Settings.Common;

internal static class SettingsMapping
{
    public static SettingsResponse ToResponse(this SystemSettings s) =>
        new(s.SessionHours, s.CommentEditHours, s.AttachmentMaxMb, [.. s.AttachmentExtensions], s.AllowCrossProjectRelations, s.AllowSelfApproval, s.TimeZone,
            s.AuditRetentionDays, s.ProjectsPrivateByDefault, [.. s.ProgressIncludedTypeIds], s.Version);
}
