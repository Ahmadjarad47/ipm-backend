using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ipm.Infrastructure.Persistence.Seeding;

/// <summary>
/// Specs 02 and 03 §6. Always: the 12 default statuses, the 3 default workflows, the 5 default work item types (bound to their
/// workflow, with their fields and hierarchy) and the 3 default relation types, the same as the front end demo data.
/// Idempotent: it adds only what is missing (by key) and never touches something an administrator has edited, except that it
/// binds a type that has no workflow yet (a row from before part 03). It needs no configuration.
/// </summary>
public static class ConfigSeeder
{
    /// <summary>Fixed ids of the default work item types.</summary>
    public static class TypeIds
    {
        public static readonly Guid Feature = new("01970000-0000-7000-8003-000000000001");   // t-feature
        public static readonly Guid Task = new("01970000-0000-7000-8003-000000000002");      // t-task
        public static readonly Guid Bug = new("01970000-0000-7000-8003-000000000003");       // t-bug
        public static readonly Guid Subtask = new("01970000-0000-7000-8003-000000000004");   // t-subtask
        public static readonly Guid Milestone = new("01970000-0000-7000-8003-000000000005"); // t-milestone
    }

    /// <summary>Fixed ids of the default relation types.</summary>
    public static class RelationTypeIds
    {
        public static readonly Guid Blocks = new("01970000-0000-7000-8004-000000000001");     // rt-blocks
        public static readonly Guid Relates = new("01970000-0000-7000-8004-000000000002");    // rt-relates
        public static readonly Guid Duplicates = new("01970000-0000-7000-8004-000000000003"); // rt-duplicates
    }

    /// <summary>Fixed ids of the default workflows.</summary>
    public static class WorkflowIds
    {
        public static readonly Guid Project = new("01970000-0000-7000-8006-000000000001");  // wf-project
        public static readonly Guid Standard = new("01970000-0000-7000-8006-000000000002"); // wf-standard
        public static readonly Guid Bug = new("01970000-0000-7000-8006-000000000003");      // wf-bug
    }

    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        _ = configuration;
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await SeedStatusesAsync(db, cancellationToken);
        await SeedWorkflowsAsync(db, cancellationToken);
        await SeedWorkItemTypesAsync(db, cancellationToken);
        await SeedRelationTypesAsync(db, cancellationToken);
    }

    private static Guid StatusId(int number) => new($"01970000-0000-7000-8005-{number:D12}");

    private static async Task SeedStatusesAsync(ApplicationDbContext db, CancellationToken cancellationToken)
    {
        const StatusScope project = StatusScope.Project;
        const StatusScope item = StatusScope.WorkItem;

        var defaults = new (int Number, StatusScope Scope, string Key, string Name, string Description, string Color, StatusCategory Category, int Order)[]
        {
            (1, project, "pending", "قيد الإعداد", "بانتظار الاعتماد والبدء", "#8a94a0", StatusCategory.NotStarted, 1),
            (2, project, "active", "نشط", "قيد التنفيذ", "#1a7a5c", StatusCategory.Active, 2),
            (3, project, "on_hold", "متوقف مؤقتاً", "معلّق لسبب موثق", "#b7791f", StatusCategory.Blocked, 3),
            (4, project, "completed", "مكتمل", "أُغلق بعد الاعتماد", "#2f6fb5", StatusCategory.Done, 4),
            (5, project, "cancelled", "ملغى", "", "#c0392b", StatusCategory.Cancelled, 5),
            (6, item, "pending", "جديد", "لم يبدأ العمل", "#8a94a0", StatusCategory.NotStarted, 1),
            (7, item, "in_progress", "قيد التنفيذ", "", "#1a7a5c", StatusCategory.Active, 2),
            (8, item, "in_review", "قيد المراجعة", "", "#7c5cbf", StatusCategory.Active, 3),
            (9, item, "blocked", "محجوب", "بانتظار اعتمادية", "#b7791f", StatusCategory.Blocked, 4),
            (10, item, "completed", "مكتمل", "", "#2f6fb5", StatusCategory.Done, 5),
            (11, item, "rejected", "مرفوض", "", "#a0522d", StatusCategory.Cancelled, 6),
            (12, item, "cancelled", "ملغى", "", "#c0392b", StatusCategory.Cancelled, 7),
        };

        var existing = await db.Set<Status>().Select(s => new { s.Scope, s.Key }).ToListAsync(cancellationToken);
        foreach (var d in defaults.Where(d => !existing.Any(e => e.Scope == d.Scope && e.Key == d.Key)))
        {
            db.Add(Status.Create(d.Key, d.Name, d.Description, d.Color, d.Category, d.Scope, d.Order, true, StatusId(d.Number)).ValueOrThrow());
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedWorkflowsAsync(ApplicationDbContext db, CancellationToken cancellationToken)
    {
        var ids = (await db.Set<Status>().Select(s => new { s.Id, s.Scope, s.Key }).ToListAsync(cancellationToken))
            .ToDictionary(s => (s.Scope, s.Key), s => s.Id);
        Guid Project(string key) => ids[(StatusScope.Project, key)];
        Guid Item(string key) => ids[(StatusScope.WorkItem, key)];

        static WorkflowTransition Move(Guid from, Guid to, string? permission = null, bool assignee = false, bool approval = false, bool dependencies = false) =>
            new(from, to, permission, assignee, approval, dependencies);

        var project = (WorkflowIds.Project, "project_default", "سير عمل المشاريع", StatusScope.Project,
            new[] { Project("pending"), Project("active"), Project("on_hold"), Project("completed"), Project("cancelled") }, Project("pending"),
            new[]
            {
                Move(Project("pending"), Project("active"), approval: true),
                Move(Project("active"), Project("on_hold")),
                Move(Project("on_hold"), Project("active")),
                Move(Project("active"), Project("completed"), approval: true),
                Move(Project("pending"), Project("cancelled")),
                Move(Project("on_hold"), Project("cancelled")),
            });

        var standard = (WorkflowIds.Standard, "standard", "سير العمل القياسي", StatusScope.WorkItem,
            new[] { Item("pending"), Item("in_progress"), Item("in_review"), Item("blocked"), Item("completed"), Item("cancelled") }, Item("pending"),
            new[]
            {
                Move(Item("pending"), Item("in_progress"), assignee: true),
                Move(Item("in_progress"), Item("in_review")),
                Move(Item("in_progress"), Item("blocked")),
                Move(Item("blocked"), Item("in_progress")),
                Move(Item("in_review"), Item("in_progress")),
                Move(Item("in_review"), Item("completed"), dependencies: true),
                Move(Item("completed"), Item("in_progress"), Permissions.TasksEdit),
                Move(Item("pending"), Item("cancelled")),
                Move(Item("in_progress"), Item("cancelled")),
            });

        var bug = (WorkflowIds.Bug, "bug", "سير عمل الأخطاء", StatusScope.WorkItem,
            new[] { Item("pending"), Item("in_progress"), Item("in_review"), Item("completed"), Item("rejected") }, Item("pending"),
            new[]
            {
                Move(Item("pending"), Item("in_progress"), assignee: true),
                Move(Item("pending"), Item("rejected")),
                Move(Item("in_progress"), Item("in_review")),
                Move(Item("in_review"), Item("in_progress")),
                Move(Item("in_review"), Item("completed"), dependencies: true),
                Move(Item("completed"), Item("in_progress"), Permissions.TasksEdit),
            });

        var existing = await db.Set<Workflow>().Select(w => w.Key).ToListAsync(cancellationToken);
        foreach (var d in new[] { project, standard, bug }.Where(d => !existing.Contains(d.Item2)))
        {
            db.Add(Workflow.Create(d.Item2, d.Item3, d.Item4, d.Item5, d.Item6, d.Item7, d.Item1).ValueOrThrow());
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedWorkItemTypesAsync(ApplicationDbContext db, CancellationToken cancellationToken)
    {
        var workflows = await db.Set<Workflow>().Select(w => new { w.Id, w.Key }).ToDictionaryAsync(w => w.Key, w => w.Id, cancellationToken);
        FieldOption[] value = [new("high", "عالية"), new("medium", "متوسطة"), new("low", "منخفضة")];
        FieldOption[] severity = [new("blocker", "معطِّل"), new("major", "كبير"), new("minor", "بسيط")];

        var defaults = new (Guid Id, string Key, string Name, string Description, string Color, WorkItemIcon Icon, int Order, string Workflow, FieldDefinition[] Fields, Guid[] Children)[]
        {
            (TypeIds.Feature, "feature", "ميزة", "قدرة وظيفية تقدم قيمة للمستخدم", "#7c5cbf", WorkItemIcon.Feature, 1, "standard",
                [
                    new("business_value", "القيمة للأعمال", FieldType.Select, true, value, null, null, null, true, true, 0),
                    new("target_release", "الإصدار المستهدف", FieldType.Text, false, null, null, null, "مثال: 2026.Q4", false, false, 1),
                ],
                [TypeIds.Task, TypeIds.Bug]),
            (TypeIds.Task, "task", "مهمة", "وحدة عمل قابلة للتنفيذ", "#1a7a5c", WorkItemIcon.Task, 2, "standard",
                [new("estimate_hours", "التقدير (ساعات)", FieldType.Number, false, null, 0, 400, null, false, false, 0)],
                [TypeIds.Subtask, TypeIds.Bug]),
            (TypeIds.Bug, "bug", "خطأ", "خلل في سلوك النظام", "#c0392b", WorkItemIcon.Bug, 3, "bug",
                [
                    new("severity", "الخطورة", FieldType.Select, true, severity, null, null, null, true, true, 0),
                    new("environment", "البيئة", FieldType.Text, false, null, null, null, null, false, false, 1),
                    new("reproducible", "قابل لإعادة الإنتاج", FieldType.Boolean, false, null, null, null, null, false, false, 2),
                ],
                [TypeIds.Subtask]),
            (TypeIds.Subtask, "subtask", "مهمة فرعية", "جزء من مهمة", "#2f6fb5", WorkItemIcon.Subtask, 4, "standard", [], []),
            (TypeIds.Milestone, "milestone", "معلم رئيسي", "نقطة تسليم مهمة", "#c9a24a", WorkItemIcon.Milestone, 5, "standard",
                [new("reviewer", "مسؤول المراجعة", FieldType.User, false, null, null, null, null, false, false, 0)],
                [TypeIds.Feature]),
        };

        var existing = await db.Set<WorkItemType>().ToListAsync(cancellationToken);
        foreach (var d in defaults)
        {
            var type = existing.Find(t => t.Key == d.Key);
            if (type is null)
            {
                type = WorkItemType.Create(d.Key, d.Name, d.Description, d.Color, d.Icon, d.Order, true, d.Id).ValueOrThrow();
                type.SetFields(d.Fields).ThrowIfFailure();
                type.SetAllowedChildren(d.Children);
                db.Add(type);
            }

            // A type from before part 03 has no workflow yet: bind it. One an administrator already bound is left alone.
            if (type.WorkflowId is null)
            {
                type.SetWorkflow(workflows[d.Workflow]);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedRelationTypesAsync(ApplicationDbContext db, CancellationToken cancellationToken)
    {
        var defaults = new (Guid Id, string Key, string Name, string Inverse, bool Directed, bool Acyclic)[]
        {
            (RelationTypeIds.Blocks, "blocks", "يحجب", "محجوب بواسطة", true, true),
            (RelationTypeIds.Relates, "relates", "مرتبط بـ", "مرتبط بـ", false, false),
            (RelationTypeIds.Duplicates, "duplicates", "يكرر", "مكرر بواسطة", true, true),
        };

        var existing = await db.Set<RelationType>().Select(t => t.Key).ToListAsync(cancellationToken);
        foreach (var d in defaults.Where(d => !existing.Contains(d.Key)))
        {
            db.Add(RelationType.Create(d.Key, d.Name, d.Inverse, d.Directed, d.Acyclic, true, [], d.Id).ValueOrThrow());
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The seed data is code we control: a failure here is a bug, not a user error.</summary>
    private static void ThrowIfFailure(this Result result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Seeding failed: {result.Error.Description}");
        }
    }

    private static T ValueOrThrow<T>(this Result<T> result)
    {
        result.ThrowIfFailure();
        return result.Value;
    }
}
