namespace Ipm.Domain.Identity;

/// <summary>
/// The 42 permission keys of the system, identical to the front end's <c>PERMISSION_GROUPS</c> (SRS §2.3).
/// They are code, not data: every endpoint names the one it needs; who holds them is configured through roles.
/// Adding a key here gives it to the built-in <c>system_admin</c> role at the next start (the seeder syncs it).
/// </summary>
public static class Permissions
{
    public const string ProjectsView = "projects.view";
    public const string ProjectsCreate = "projects.create";
    public const string ProjectsEdit = "projects.edit";
    public const string ProjectsArchive = "projects.archive";
    public const string ProjectsMembersManage = "projects.members.manage";

    public const string TasksView = "tasks.view";
    public const string TasksCreate = "tasks.create";
    public const string TasksEdit = "tasks.edit";
    public const string TasksAssign = "tasks.assign";
    public const string TasksDelete = "tasks.delete";

    public const string StatusesManage = "statuses.manage";
    public const string WorkflowsManage = "workflows.manage";
    public const string WorkflowTransition = "workflow.transition";

    public const string ApprovalsRequest = "approvals.request";
    public const string ApprovalsView = "approvals.view";
    public const string ApprovalsDecide = "approvals.decide";
    public const string ApprovalsConfigure = "approvals.configure";

    public const string CommentsCreate = "comments.create";
    public const string CommentsEditOwn = "comments.edit_own";
    public const string FilesUpload = "files.upload";
    public const string FilesDownload = "files.download";
    public const string FilesDelete = "files.delete";

    public const string GitView = "git.view";
    public const string GitManage = "git.manage";
    public const string GitSync = "git.sync";

    public const string RolesManage = "roles.manage";
    public const string PermissionsAssign = "permissions.assign";
    public const string AuditView = "audit.view";
    public const string AuditExport = "audit.export";
    public const string AuditOverride = "audit.override";
    public const string SettingsManage = "settings.manage";

    public const string DashboardView = "dashboard.view";
    public const string ReportsView = "reports.view";
    public const string ReportsExport = "reports.export";

    public const string WorkItemTypesView = "workitem.types.view";
    public const string WorkItemTypesManage = "workitem.types.manage";
    public const string WorkItemFieldsManage = "workitem.fields.manage";
    public const string ProjectWorkItemTypesConfigure = "project.workitemtypes.configure";

    public const string WorkItemRelationsView = "workitem.relations.view";
    public const string WorkItemRelationsCreate = "workitem.relations.create";
    public const string WorkItemRelationsDelete = "workitem.relations.delete";
    public const string WorkItemRelationTypesManage = "workitem.relationtypes.manage";

    public static readonly IReadOnlyList<PermissionGroup> Groups =
    [
        Group("projects", "المشاريع",
            (ProjectsView, "عرض المشاريع التي أنت عضو فيها"),
            (ProjectsCreate, "إنشاء مشاريع جديدة"),
            (ProjectsEdit, "تعديل بيانات المشروع وإعداداته"),
            (ProjectsArchive, "أرشفة المشروع واستعادته"),
            (ProjectsMembersManage, "إضافة أعضاء المشروع وتغيير أدوارهم وإزالتهم")),
        Group("tasks", "المهام",
            (TasksView, "عرض الأعمال"),
            (TasksCreate, "إنشاء أعمال جديدة"),
            (TasksEdit, "تعديل بيانات الأعمال"),
            (TasksAssign, "تكليف الأعضاء بالأعمال"),
            (TasksDelete, "أرشفة الأعمال")),
        Group("workflow", "سير العمل",
            (StatusesManage, "إدارة الحالات: إنشاؤها وتعديلها وتعطيلها"),
            (WorkflowsManage, "إدارة مسارات العمل والانتقالات المسموحة"),
            (WorkflowTransition, "نقل العمل بين الحالات")),
        Group("approvals", "الموافقات",
            (ApprovalsRequest, "إرسال طلبات موافقة"),
            (ApprovalsView, "عرض طلبات الموافقة ومسارها"),
            (ApprovalsDecide, "اتخاذ قرار الموافقة أو الرفض أو الإعادة"),
            (ApprovalsConfigure, "إعداد قوالب الموافقات ومراحلها")),
        Group("collaboration", "التعاون",
            (CommentsCreate, "إضافة تعليقات"),
            (CommentsEditOwn, "تعديل تعليقاتك خلال المهلة المحددة"),
            (FilesUpload, "رفع المرفقات"),
            (FilesDownload, "تنزيل المرفقات"),
            (FilesDelete, "حذف المرفقات")),
        Group("integration", "تكامل Git",
            (GitView, "عرض مستودعات Git وأحداثها"),
            (GitManage, "ربط المستودعات وإدارة الويب هوك"),
            (GitSync, "مزامنة أحداث Git وإعادة إرسالها")),
        Group("security", "الأمن والتدقيق",
            (RolesManage, "إدارة الأدوار: إنشاؤها وتعديلها وحذفها"),
            (PermissionsAssign, "إدارة المستخدمين وتعيين الأدوار لهم"),
            (AuditView, "عرض سجل التدقيق الكامل، بما فيه الأحداث الأمنية"),
            (AuditExport, "تصدير سجل التدقيق"),
            (AuditOverride, "تجاوز القيود المحمية، وكل استخدام له يُسجَّل"),
            (SettingsManage, "إدارة إعدادات النظام وقوالب المشاريع")),
        Group("reporting", "التقارير",
            (DashboardView, "عرض لوحة القيادة"),
            (ReportsView, "عرض التقارير والخط الزمني"),
            (ReportsExport, "تصدير التقارير")),
        Group("workItemTypes", "أنواع الأعمال",
            (WorkItemTypesView, "عرض أنواع الأعمال"),
            (WorkItemTypesManage, "إدارة أنواع الأعمال وقواعد التسلسل الهرمي"),
            (WorkItemFieldsManage, "إدارة الحقول الإضافية لأنواع الأعمال"),
            (ProjectWorkItemTypesConfigure, "تحديد الأنواع المسموحة والافتراضية داخل المشروع")),
        Group("relationships", "العلاقات",
            (WorkItemRelationsView, "عرض الروابط بين الأعمال"),
            (WorkItemRelationsCreate, "إنشاء روابط بين الأعمال"),
            (WorkItemRelationsDelete, "حذف الروابط بين الأعمال"),
            (WorkItemRelationTypesManage, "إدارة أنواع الروابط وأزواج الأنواع المسموحة")),
    ];

    /// <summary>Every key, in catalog order.</summary>
    public static readonly IReadOnlyList<string> All = [.. Groups.SelectMany(g => g.Permissions.Select(p => p.Key))];

    private static readonly HashSet<string> Known = [.. All];

    public static bool IsKnown(string permission) => Known.Contains(permission);

    private static PermissionGroup Group(string group, string label, params (string Key, string Label)[] permissions) =>
        new(group, label, [.. permissions.Select(p => new PermissionInfo(p.Key, p.Label))]);
}
