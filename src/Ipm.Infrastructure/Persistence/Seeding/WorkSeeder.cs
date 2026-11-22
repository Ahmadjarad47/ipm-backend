using System.Text.Json;
using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Ipm.Domain.Projects;
using Ipm.Domain.Work;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ipm.Infrastructure.Persistence.Seeding;

/// <summary>
/// Spec 05 §6 and 05b §6. Nothing is seeded always (work items are operational data). Development only (<c>Seed:Demo</c>): the sample work items of
/// the four demo projects of the front end, with their types, statuses, priorities, assignees, parents, custom fields and dates relative
/// to today. Idempotent: a project that already has items is left alone. The items are written in their final status directly,
/// without a status history, because they are sample data.
/// </summary>
public static class WorkSeeder
{
    private static Guid ItemId(int project, int number) => new($"01970000-0000-7000-8009-{project * 1000 + number:D12}");

    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue<bool>("Seed:Demo"))
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var users = await db.Users.Select(u => new { u.Id, u.UserName }).ToDictionaryAsync(u => u.UserName!, u => u.Id, cancellationToken);
        var types = await db.Set<WorkItemType>().ToDictionaryAsync(t => t.Key, t => t.Id, cancellationToken);
        var statuses = await db.Set<Status>().Where(s => s.Scope == StatusScope.WorkItem).ToDictionaryAsync(s => s.Key, s => s.Id, cancellationToken);
        var projects = await db.Set<Project>().ToDictionaryAsync(p => p.Code, cancellationToken);

        var demo = new (int Number, string Code, Seed[] Items)[]
        {
            (1, "ERP", ErpItems),
            (2, "SSP", SspItems),
            (3, "BI", BiItems),
            (4, "MAIL", MailItems),
        };

        foreach (var (number, code, seeds) in demo)
        {
            if (!projects.TryGetValue(code, out var project)
                || await db.Set<WorkItem>().AnyAsync(i => i.ProjectId == project.Id, cancellationToken)
                || seeds.Any(s => s.Assignees.Any(a => !users.ContainsKey(a))))
            {
                continue;
            }

            foreach (var s in seeds)
            {
                var fields = s.Fields.ToDictionary(f => f.Key, f => JsonSerializer.SerializeToElement(f.Value));
                var item = WorkItem.Create(
                    project.Id, project.Code, 100 + s.N, types[s.Type], statuses[s.Status], s.Title,
                    "وصف تفصيلي للعمل المطلوب ومعايير القبول.", s.Priority,
                    s.Due is { } due ? today.AddDays(due - 14) : null, s.Due is { } d ? today.AddDays(d) : null,
                    s.Parent is { } parent ? ItemId(number, parent) : null, s.Assignees.Select(a => users[a]), fields, ItemId(number, s.N));
                if (item.IsFailure)
                {
                    throw new InvalidOperationException($"Seeding failed: {item.Error.Description}");
                }

                db.Add(item.Value);
            }

            await db.SaveChangesAsync(cancellationToken);

            // The counter continues after the seeded numbers (R1), so the next created item is the next number.
            var last = 100 + seeds.Max(s => s.N);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO work.project_counters (project_id, last_number) VALUES ({project.Id}, {last})
                ON CONFLICT (project_id) DO UPDATE SET last_number = GREATEST(work.project_counters.last_number, {last})
                """,
                cancellationToken);
        }

        await SeedRelationsAndCommentsAsync(db, users, cancellationToken);
    }

    /// <summary>Spec 05b §6: the four links and the three comments of the front end sample data. Skipped when the table already has rows.</summary>
    private static async Task SeedRelationsAndCommentsAsync(
        ApplicationDbContext db, Dictionary<string, Guid> users, CancellationToken cancellationToken)
    {
        var kinds = await db.Set<RelationType>().ToDictionaryAsync(t => t.Key, t => t.Id, cancellationToken);
        var existingItems = (await db.Set<WorkItem>().Select(i => i.Id).ToListAsync(cancellationToken)).ToHashSet();

        if (!await db.Set<WorkItemRelation>().AnyAsync(cancellationToken))
        {
            var links = new (int Number, int ProjectNumber, int Source, int Target, string Kind)[]
            {
                (1, 1, 9, 8, "blocks"), (2, 1, 5, 4, "blocks"), (3, 1, 10, 4, "relates"), (4, 2, 4, 2, "relates"),
            };
            foreach (var l in links.Where(l => kinds.ContainsKey(l.Kind) && existingItems.Contains(ItemId(l.ProjectNumber, l.Source)) && existingItems.Contains(ItemId(l.ProjectNumber, l.Target))))
            {
                var relation = WorkItemRelation.Create(ItemId(l.ProjectNumber, l.Source), ItemId(l.ProjectNumber, l.Target), kinds[l.Kind], new Guid($"01970000-0000-7000-8010-{l.Number:D12}"));
                db.Add(relation.Value);
            }
        }

        if (!await db.Set<Comment>().AnyAsync(cancellationToken))
        {
            var now = DateTimeOffset.UtcNow;
            var erp = await db.Set<Project>().Where(p => p.Code == "ERP").Select(p => p.Id).SingleOrDefaultAsync(cancellationToken);
            if (users.TryGetValue("reem", out var reem) && users.TryGetValue("fahad", out var fahad) && users.TryGetValue("khaled", out var khaled) && existingItems.Contains(ItemId(1, 4)) && erp != Guid.Empty)
            {
                db.Add(Comment.Create(CommentSubjectType.WorkItem, ItemId(1, 4), reem, "تم الاتفاق مع فريق المالية على أن تكون الموافقة على ثلاث مراحل.", [], now.AddDays(-3), new Guid("01970000-0000-7000-8011-000000000001")));
                db.Add(Comment.Create(CommentSubjectType.WorkItem, ItemId(1, 4), fahad, "بانتظار إصلاح خطأ الضريبة ERP-105 قبل الدمج.", [], now.AddDays(-1), new Guid("01970000-0000-7000-8011-000000000002")));
                db.Add(Comment.Create(CommentSubjectType.Project, erp, khaled, "اجتماع المراجعة الأسبوعي يوم الأحد الساعة العاشرة.", [], now.AddDays(-5), new Guid("01970000-0000-7000-8011-000000000003")));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private sealed record Seed(
        int N, string Type, string Title, string Status, Priority Priority, string[] Assignees, int? Due, int? Parent = null,
        Dictionary<string, object>? FieldValues = null)
    {
        public Dictionary<string, object> Fields => FieldValues ?? [];
    }

    private static readonly Seed[] ErpItems =
    [
        new(1, "milestone", "إطلاق وحدة المشتريات", "in_progress", Priority.High, ["khaled"], 30),
        new(2, "feature", "طلبات الشراء متعددة المراحل", "in_progress", Priority.High, ["reem"], 20, 1,
            new() { ["business_value"] = "high", ["target_release"] = "2026.Q4" }),
        new(3, "task", "تصميم شاشة إنشاء طلب الشراء", "completed", Priority.Medium, ["fahad"], -5, 2, new() { ["estimate_hours"] = 16 }),
        new(4, "task", "ربط طلب الشراء بسير الموافقات", "in_progress", Priority.High, ["fahad", "reem"], 6, 2, new() { ["estimate_hours"] = 24 }),
        new(5, "bug", "خطأ في احتساب ضريبة القيمة المضافة", "in_review", Priority.Critical, ["fahad"], -2, 2,
            new() { ["severity"] = "blocker", ["environment"] = "staging", ["reproducible"] = true }),
        new(6, "subtask", "اختبارات الوحدة لمحرك الضريبة", "pending", Priority.Medium, [], 8, 4),
        new(7, "feature", "إدارة الموردين", "pending", Priority.Medium, ["reem"], 45, 1, new() { ["business_value"] = "medium" }),
        new(8, "task", "نموذج تسجيل مورد جديد", "pending", Priority.Medium, [], 35, 7),
        new(9, "task", "ترحيل بيانات الموردين القديمة", "blocked", Priority.High, ["fahad"], -1, 7, new() { ["estimate_hours"] = 30 }),
        new(10, "bug", "تعذّر رفع مرفقات بحجم كبير", "in_progress", Priority.Medium, ["fahad"], 4, null,
            new() { ["severity"] = "major", ["environment"] = "production" }),
        new(11, "task", "توثيق واجهات API للمخزون", "completed", Priority.Low, ["reem"], -10),
        new(12, "task", "تحسين أداء تقرير المخزون", "in_review", Priority.Medium, ["fahad"], 2),
    ];

    private static readonly Seed[] SspItems =
    [
        new(1, "feature", "طلب إجازة إلكتروني", "in_progress", Priority.High, ["reem"], 15, null, new() { ["business_value"] = "high" }),
        new(2, "task", "نموذج الطلب والتحقق من الرصيد", "completed", Priority.High, ["reem"], -3, 1),
        new(3, "task", "إشعارات البريد للمدير المباشر", "in_progress", Priority.Medium, ["reem"], 5, 1),
        new(4, "bug", "اتجاه التاريخ معكوس في التقويم", "pending", Priority.Low, [], 12, 1, new() { ["severity"] = "minor" }),
        new(5, "feature", "شهادة تعريف بالراتب", "pending", Priority.Medium, [], 30, null, new() { ["business_value"] = "medium" }),
    ];

    private static readonly Seed[] BiItems =
    [
        new(1, "milestone", "النموذج الأولي للوحة الإدارة", "pending", Priority.High, ["noura"], 60),
        new(2, "feature", "مؤشرات الأداء المالية", "pending", Priority.High, ["fahad"], 50, 1, new() { ["business_value"] = "high" }),
        new(3, "task", "تحليل مصادر البيانات", "in_progress", Priority.Medium, ["fahad"], 20, 2),
    ];

    private static readonly Seed[] MailItems =
    [
        new(1, "task", "ترحيل صناديق الإدارة العليا", "completed", Priority.High, ["fahad"], -40),
        new(2, "task", "ترحيل الصناديق المشتركة", "completed", Priority.Medium, ["fahad"], -25),
    ];
}
