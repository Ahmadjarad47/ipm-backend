using Ipm.Domain.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ipm.Infrastructure.Persistence.Seeding;

/// <summary>
/// Spec 02 §6. Always: the five default work item types (with their fields and hierarchy) and the three default relation
/// types, the same as the front end demo data. Idempotent: it adds only the types whose key is missing and never touches
/// one an administrator has edited. It needs no configuration.
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

    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        _ = configuration;
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await SeedWorkItemTypesAsync(db, cancellationToken);
        await SeedRelationTypesAsync(db, cancellationToken);
    }

    private static async Task SeedWorkItemTypesAsync(ApplicationDbContext db, CancellationToken cancellationToken)
    {
        FieldOption[] value = [new("high", "عالية"), new("medium", "متوسطة"), new("low", "منخفضة")];
        FieldOption[] severity = [new("blocker", "معطِّل"), new("major", "كبير"), new("minor", "بسيط")];

        var defaults = new (Guid Id, string Key, string Name, string Description, string Color, WorkItemIcon Icon, int Order, FieldDefinition[] Fields, Guid[] Children)[]
        {
            (TypeIds.Feature, "feature", "ميزة", "قدرة وظيفية تقدم قيمة للمستخدم", "#7c5cbf", WorkItemIcon.Feature, 1,
                [
                    new("business_value", "القيمة للأعمال", FieldType.Select, true, value, null, null, null, true, true, 0),
                    new("target_release", "الإصدار المستهدف", FieldType.Text, false, null, null, null, "مثال: 2026.Q4", false, false, 1),
                ],
                [TypeIds.Task, TypeIds.Bug]),
            (TypeIds.Task, "task", "مهمة", "وحدة عمل قابلة للتنفيذ", "#1a7a5c", WorkItemIcon.Task, 2,
                [new("estimate_hours", "التقدير (ساعات)", FieldType.Number, false, null, 0, 400, null, false, false, 0)],
                [TypeIds.Subtask, TypeIds.Bug]),
            (TypeIds.Bug, "bug", "خطأ", "خلل في سلوك النظام", "#c0392b", WorkItemIcon.Bug, 3,
                [
                    new("severity", "الخطورة", FieldType.Select, true, severity, null, null, null, true, true, 0),
                    new("environment", "البيئة", FieldType.Text, false, null, null, null, null, false, false, 1),
                    new("reproducible", "قابل لإعادة الإنتاج", FieldType.Boolean, false, null, null, null, null, false, false, 2),
                ],
                [TypeIds.Subtask]),
            (TypeIds.Subtask, "subtask", "مهمة فرعية", "جزء من مهمة", "#2f6fb5", WorkItemIcon.Subtask, 4, [], []),
            (TypeIds.Milestone, "milestone", "معلم رئيسي", "نقطة تسليم مهمة", "#c9a24a", WorkItemIcon.Milestone, 5,
                [new("reviewer", "مسؤول المراجعة", FieldType.User, false, null, null, null, null, false, false, 0)],
                [TypeIds.Feature]),
        };

        var existing = await db.Set<WorkItemType>().Select(t => t.Key).ToListAsync(cancellationToken);
        foreach (var d in defaults.Where(d => !existing.Contains(d.Key)))
        {
            var type = WorkItemType.Create(d.Key, d.Name, d.Description, d.Color, d.Icon, d.Order, true, d.Id).Value;
            type.SetFields(d.Fields).ThrowIfFailure();
            type.SetAllowedChildren(d.Children);
            db.Add(type);
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
            db.Add(RelationType.Create(d.Key, d.Name, d.Inverse, d.Directed, d.Acyclic, true, [], d.Id).Value);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The seed data is code we control: a failure here is a bug, not a user error.</summary>
    private static void ThrowIfFailure(this Ipm.Domain.Common.Result result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Seeding failed: {result.Error.Description}");
        }
    }
}
