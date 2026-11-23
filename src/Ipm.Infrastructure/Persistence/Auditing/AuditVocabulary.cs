using System.Text.Json;
using Ipm.Application.Abstractions.Auditing;
using Ipm.Domain.Identity;

namespace Ipm.Infrastructure.Persistence.Auditing;

/// <summary>
/// Spec 08 R1: names the raw rows of <c>audit_log</c>. A table, the kind of change and (for updates) a column that changed give a fixed action key
/// such as <c>workItem.transition</c>; the first matching rule wins, and the last rule of a table is its plain "created / updated / deleted".
/// </summary>
internal static class AuditVocabulary
{
    internal sealed record Rule(string Table, AuditAction Action, string? Column, string Key, string SubjectType, string Label);

    internal static readonly IReadOnlyList<Rule> Rules =
    [
        new("projects.projects", AuditAction.Insert, null, "project.created", "project", "أنشأ مشروعًا"),
        new("projects.projects", AuditAction.Update, "isArchived", "project.archived", "project", "أرشف أو استعاد مشروعًا"),
        new("projects.projects", AuditAction.Update, "statusId", "project.transition", "project", "غيّر حالة مشروع"),
        new("projects.projects", AuditAction.Update, null, "project.updated", "project", "عدّل مشروعًا"),
        new("projects.projects", AuditAction.Delete, null, "project.deleted", "project", "حذف مشروعًا"),
        new("projects.project_members", AuditAction.Insert, null, "project.members", "project", "عدّل أعضاء مشروع"),
        new("projects.project_members", AuditAction.Update, null, "project.members", "project", "عدّل أعضاء مشروع"),
        new("projects.project_members", AuditAction.Delete, null, "project.members", "project", "عدّل أعضاء مشروع"),
        new("work.work_items", AuditAction.Insert, null, "workItem.created", "workItem", "أنشأ عنصر عمل"),
        new("work.work_items", AuditAction.Update, "statusId", "workItem.transition", "workItem", "غيّر حالة عنصر عمل"),
        new("work.work_items", AuditAction.Update, "assigneeIds", "workItem.assigned", "workItem", "غيّر المكلّفين"),
        new("work.work_items", AuditAction.Update, "parentId", "workItem.parent", "workItem", "غيّر العنصر الأب"),
        new("work.work_items", AuditAction.Update, "typeId", "workItem.typeChanged", "workItem", "غيّر نوع عنصر عمل"),
        new("work.work_items", AuditAction.Update, null, "workItem.updated", "workItem", "عدّل عنصر عمل"),
        new("work.work_items", AuditAction.Delete, null, "workItem.deleted", "workItem", "حذف عنصر عمل"),
        new("work.work_item_relations", AuditAction.Insert, null, "relation.created", "relation", "أنشأ رابطًا"),
        new("work.work_item_relations", AuditAction.Update, null, "relation.updated", "relation", "عدّل رابطًا"),
        new("work.work_item_relations", AuditAction.Delete, null, "relation.deleted", "relation", "حذف رابطًا"),
        new("work.comments", AuditAction.Insert, null, "comment.created", "comment", "أضاف تعليقًا"),
        new("work.comments", AuditAction.Update, "isDeleted", "comment.deleted", "comment", "حذف تعليقًا"),
        new("work.comments", AuditAction.Update, null, "comment.edited", "comment", "عدّل تعليقًا"),
        new("work.comments", AuditAction.Delete, null, "comment.deleted", "comment", "حذف تعليقًا"),
        new("approvals.approval_requests", AuditAction.Insert, null, "approval.submitted", "approval", "قدّم طلب موافقة"),
        new("approvals.approval_requests", AuditAction.Update, null, "approval.updated", "approval", "حدّث طلب موافقة"),
        new("approvals.approval_templates", AuditAction.Insert, null, "approval-template.created", "approval-template", "أنشأ قالب موافقة"),
        new("approvals.approval_templates", AuditAction.Update, null, "approval-template.updated", "approval-template", "عدّل قالب موافقة"),
        new("approvals.delegations", AuditAction.Insert, null, "delegation.created", "delegation", "أنشأ تفويضًا"),
        new("approvals.delegations", AuditAction.Update, null, "delegation.updated", "delegation", "عدّل تفويضًا"),
        new("git.repositories", AuditAction.Insert, null, "git.connected", "git", "ربط مستودع Git"),
        new("git.repositories", AuditAction.Update, "isConnected", "git.disconnected", "git", "فصل مستودع Git"),
        new("git.repositories", AuditAction.Update, null, "git.updated", "git", "عدّل مستودع Git"),
        new("git.git_events", AuditAction.Insert, null, "git.event", "git", "سجّل حدث Git"),
        new("git.git_events", AuditAction.Update, null, "git.linked", "git", "حدّث ربط حدث Git"),
        new("identity.users", AuditAction.Insert, null, "user.created", "user", "أنشأ مستخدمًا"),
        new("identity.users", AuditAction.Update, null, "user.updated", "user", "عدّل بيانات مستخدم"),
        new("identity.roles", AuditAction.Insert, null, "role.created", "role", "أنشأ دورًا"),
        new("identity.roles", AuditAction.Update, null, "role.updated", "role", "عدّل دورًا"),
        new("identity.roles", AuditAction.Delete, null, "role.deleted", "role", "حذف دورًا"),
        new("config.work_item_types", AuditAction.Insert, null, "type.created", "type", "أنشأ نوع عنصر"),
        new("config.work_item_types", AuditAction.Update, null, "type.updated", "type", "عدّل نوع عنصر"),
        new("config.statuses", AuditAction.Insert, null, "status.created", "status", "أنشأ حالة"),
        new("config.statuses", AuditAction.Update, null, "status.updated", "status", "عدّل حالة"),
        new("config.workflows", AuditAction.Insert, null, "workflow.created", "workflow", "أنشأ سير عمل"),
        new("config.workflows", AuditAction.Update, null, "workflow.updated", "workflow", "عدّل سير عمل"),
        new("config.system_settings", AuditAction.Update, null, "settings.updated", "settings", "عدّل إعدادات النظام"),
        new("config.relation_types", AuditAction.Insert, null, "relation-type.created", "relation-type", "أنشأ نوع رابط"),
        new("audit.exports", AuditAction.Insert, null, "audit.exported", "audit", "صدّر سجل التدقيق"),
        new("config.relation_types", AuditAction.Update, null, "relation-type.updated", "relation-type", "عدّل نوع رابط"),
    ];

    /// <summary>The security events are shown beside the audit log (R3) under these keys.</summary>
    internal static readonly IReadOnlyDictionary<SecurityEventType, (string Key, string Label)> Security = new Dictionary<SecurityEventType, (string, string)>
    {
        [SecurityEventType.LoginSucceeded] = ("auth.login", "سجّل الدخول"),
        [SecurityEventType.LoginFailed] = ("auth.login_failed", "محاولة دخول مرفوضة"),
        [SecurityEventType.LockedOut] = ("auth.locked_out", "قُفل الحساب بعد محاولات فاشلة"),
        [SecurityEventType.PermissionDenied] = ("auth.permission_denied", "محاولة بدون صلاحية"),
        [SecurityEventType.PasswordChanged] = ("auth.password_changed", "غيّر كلمة المرور"),
        [SecurityEventType.PasswordReset] = ("auth.password_reset", "أعاد تعيين كلمة المرور"),
        [SecurityEventType.SessionsRevoked] = ("auth.sessions_revoked", "أنهى الجلسات"),
    };

    private static readonly string[] Stamps = ["createdAt", "createdBy", "updatedAt", "updatedBy"];

    /// <summary>The rule of a stored row: the first one of its table and action whose column is among the changed ones.</summary>
    public static (string Key, string SubjectType, string Label) Describe(string entity, AuditAction action, string? after)
    {
        HashSet<string>? columns = null;
        foreach (var rule in Rules.Where(r => r.Table == entity && r.Action == action))
        {
            columns ??= Columns(after);
            if (rule.Column is null || columns.Contains(rule.Column))
            {
                return (rule.Key, rule.SubjectType, rule.Label);
            }
        }

        var table = entity[(entity.IndexOf('.', StringComparison.Ordinal) + 1)..];
        var verb = action switch { AuditAction.Insert => "created", AuditAction.Delete => "deleted", _ => "updated" };
        return ($"{table}.{verb}", table, verb switch { "created" => "أنشأ سجلًا", "deleted" => "حذف سجلًا", _ => "عدّل سجلًا" });
    }

    /// <summary>R1: the changed columns of an update (stamps left out); an insert or delete lists none, the action says it all.</summary>
    public static IReadOnlyList<AuditFieldChange> Changes(AuditAction action, string? before, string? after)
    {
        if (action != AuditAction.Update || after is null)
        {
            return [];
        }

        using var now = JsonDocument.Parse(after);
        using var was = JsonDocument.Parse(before ?? "{}");
        return [.. now.RootElement.EnumerateObject()
            .Where(p => !Stamps.Contains(p.Name))
            .Select(p => new AuditFieldChange(p.Name, was.RootElement.TryGetProperty(p.Name, out var old) ? Show(old) : null, Show(p.Value)))];
    }

    private static string? Show(JsonElement value) =>
        value.ValueKind == JsonValueKind.Null ? null : value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();

    private static HashSet<string> Columns(string? after)
    {
        if (after is null)
        {
            return [];
        }

        using var document = JsonDocument.Parse(after);
        return [.. document.RootElement.EnumerateObject().Select(p => p.Name)];
    }
}
