using System.Globalization;
using System.Text.Json;
using Ipm.Domain.Approvals;
using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Ipm.Domain.Identity;
using Ipm.Domain.Projects;
using Ipm.Domain.Work;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ipm.Infrastructure.Persistence.Seeding;

/// <summary>
/// Spec 06 §6 and 06b §6. Always: the three approval templates of the front end (new project, work item completion, project closing), with their
/// users replaced by positions and roles. Development only (<c>Seed:Demo</c>): the four sample requests. Idempotent: templates are added by
/// name and requests only when none exists.
/// </summary>
public static class ApprovalsSeeder
{
    private static Guid TemplateId(int number) => new($"01970000-0000-7000-8012-{number:D12}");

    private static Guid RequestId(int number) => new($"01970000-0000-7000-8013-{number:D12}");

    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var roles = await db.Set<Role>().ToDictionaryAsync(r => r.Key, r => r.Id, cancellationToken);
        var types = await db.Set<WorkItemType>().ToDictionaryAsync(t => t.Key, t => t.Id, cancellationToken);
        await SeedTemplatesAsync(db, roles, types, cancellationToken);

        if (configuration.GetValue<bool>("Seed:Demo"))
        {
            await SeedSensitiveTemplatesAsync(db, roles, types, cancellationToken);
        }

        if (configuration.GetValue<bool>("Seed:Demo") && !await db.Set<ApprovalRequest>().AnyAsync(cancellationToken))
        {
            await SeedRequestsAsync(db, cancellationToken);
        }
    }

    private static async Task SeedTemplatesAsync(ApplicationDbContext db, Dictionary<string, Guid> roles, Dictionary<string, Guid> types, CancellationToken cancellationToken)
    {
        static ApprovalStage Stage(int order, string name, ApproverKind kind, string approver, int? dueHours = null) => new(order, name, kind, approver, dueHours);
        string Role(string key) => roles[key].ToString();

        var defaults = new (int Number, string Name, ApprovalEvent Event, string[] Fields, ApprovalRule Rule, ApprovalStage[] Stages)[]
        {
            (1, "اعتماد مشروع جديد", ApprovalEvent.ProjectApprove, ["name", "startDate", "endDate", "ownerId"],
                new ApprovalRule(null, null, null, [ApprovalAction.Activate], null, null),
                [
                    Stage(1, "المسؤول المباشر", ApproverKind.Position, "direct-manager"),
                    Stage(2, "مدير المشروع", ApproverKind.Role, Role("project_manager")),
                    Stage(3, "الاعتماد النهائي", ApproverKind.Role, Role("system_admin")),
                ]),
            (2, "اعتماد إنجاز مهمة", ApprovalEvent.WorkItemApprove, ["title", "dueDate", "parentId"],
                new ApprovalRule([types["task"], types["feature"], types["bug"]], null, null, [ApprovalAction.Close, ApprovalAction.Reopen], null, null),
                [
                    Stage(1, "المشرف التقني", ApproverKind.Role, Role("approver")),
                    Stage(2, "مدير المشروع", ApproverKind.Role, Role("project_manager")),
                ]),
            (3, "إغلاق مشروع", ApprovalEvent.ProjectClose, ["endDate"],
                new ApprovalRule(null, null, null, [ApprovalAction.Close], null, null),
                [
                    Stage(1, "مدير المشروع", ApproverKind.Role, Role("project_manager")),
                    Stage(2, "الاعتماد النهائي", ApproverKind.Role, Role("system_admin")),
                ]),
        };

        var existing = await db.Set<ApprovalTemplate>().Select(t => t.Name).ToListAsync(cancellationToken);
        foreach (var d in defaults.Where(d => !existing.Contains(d.Name)))
        {
            var template = ApprovalTemplate.Create(d.Name, d.Event, true, d.Stages, d.Fields, d.Rule, false, TemplateId(d.Number));
            db.Add(template.Value);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Spec 06b §6, development only: the two sensitive-change templates of the front end. They stop direct edits of what they protect, so a fresh install does not get them.</summary>
    private static async Task SeedSensitiveTemplatesAsync(
        ApplicationDbContext db, Dictionary<string, Guid> roles, Dictionary<string, Guid> types, CancellationToken cancellationToken)
    {
        var active = await db.Set<Status>().Where(s => s.Scope == StatusScope.Project && s.Key == "active").Select(s => (Guid?)s.Id).SingleOrDefaultAsync(cancellationToken);
        if (active is null)
        {
            return;
        }

        var existing = await db.Set<ApprovalTemplate>().Select(t => t.Name).ToListAsync(cancellationToken);
        if (!existing.Contains("تعديل حساس على مهمة حرجة"))
        {
            db.Add(ApprovalTemplate.Create(
                "تعديل حساس على مهمة حرجة", ApprovalEvent.SensitiveChange, true,
                [new ApprovalStage(1, "مدير المشروع", ApproverKind.Role, roles["project_manager"].ToString(), 48)],
                ["title", "dueDate", "parentId", "priority"],
                new ApprovalRule([types["task"]], null, [Priority.Critical], [ApprovalAction.Change], null, 10), false, TemplateId(4)).Value);
        }

        if (!existing.Contains("تعديل حساس على مشروع فعّال"))
        {
            db.Add(ApprovalTemplate.Create(
                "تعديل حساس على مشروع فعّال", ApprovalEvent.SensitiveChange, true,
                [new ApprovalStage(1, "الاعتماد النهائي", ApproverKind.Role, roles["system_admin"].ToString(), null)],
                ["name", "startDate", "endDate", "ownerId"],
                new ApprovalRule(null, null, null, [ApprovalAction.Change], [new RuleCondition("statusId", "eq", JsonSerializer.SerializeToElement(active.Value.ToString()))], null),
                false, TemplateId(5)).Value);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedRequestsAsync(ApplicationDbContext db, CancellationToken cancellationToken)
    {
        var users = await db.Users.Select(u => new { u.Id, u.UserName }).ToDictionaryAsync(u => u.UserName!, u => u.Id, cancellationToken);
        var templates = await db.Set<ApprovalTemplate>().ToDictionaryAsync(t => t.Name, cancellationToken);
        var projects = await db.Set<Project>().ToDictionaryAsync(p => p.Code, cancellationToken);
        string[] needed = ["khaled", "noura", "fahad", "reem", "admin"];
        if (needed.Any(n => !users.ContainsKey(n)) || !projects.TryGetValue("BI", out var bi) || !templates.TryGetValue("اعتماد مشروع جديد", out var projectTemplate))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        ResolvedApprovers Stage(int order, params string[] names) => new(order, [.. names.Select(n => users[n])]);

        // apr-1: a new project waiting for its first stage.

        var first = ApprovalRequest.Create(
            projectTemplate, ApprovalSubjectType.Project, bi.Id, bi.Id, $"اعتماد مشروع جديد: {bi.Name}", users["khaled"], ApprovalAction.Activate, null,
            [
                new SnapshotEntry("name", bi.Name),
                new SnapshotEntry("startDate", bi.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                new SnapshotEntry("endDate", bi.EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                new SnapshotEntry("ownerId", bi.OwnerId.ToString()),
            ],
            [Stage(1, "noura"), Stage(2, "noura"), Stage(3, "admin")], now.AddDays(-2), RequestId(1));
        db.Add(first);

        var items = await db.Set<WorkItem>().ToDictionaryAsync(i => i.Ref, cancellationToken);
        var completion = templates["اعتماد إنجاز مهمة"];
        WorkItem? Item(string reference) => items.GetValueOrDefault(reference);

        void Add(int number, string reference, string requester, ApprovalAction action, ResolvedApprovers[] resolved, int daysAgo, params (int Stage, string By, DecisionKind Kind, string Comment, int DaysAgo)[] decisions)
        {
            if (Item(reference) is not { } item || !projects.Values.Any(p => p.Id == item.ProjectId))
            {
                return;
            }

            var request = ApprovalRequest.Create(
                completion, ApprovalSubjectType.WorkItem, item.Id, item.ProjectId, $"اعتماد إنجاز مهمة: {item.Ref} {item.Title}", users[requester], action, null,
                [
                    new SnapshotEntry("title", item.Title),
                    new SnapshotEntry("dueDate", item.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                    new SnapshotEntry("parentId", item.ParentId?.ToString()),
                ],
                resolved, now.AddDays(-daysAgo), RequestId(number));
            foreach (var d in decisions)
            {
                request.Decide(users[d.By], d.Kind, d.Comment, null, 1, now.AddDays(-d.DaysAgo)).ThrowIfFailure();
            }

            db.Add(request);
        }

        Add(2, "ERP-103", "fahad", ApprovalAction.Close, [Stage(1, "noura"), Stage(2, "khaled")], 3,
            (1, "noura", DecisionKind.Approve, "التصميم مطابق للهوية المعتمدة.", 1));
        Add(3, "SSP-102", "reem", ApprovalAction.Close, [Stage(1, "noura"), Stage(2, "khaled")], 6,
            (1, "noura", DecisionKind.Approve, "تم التحقق.", 4), (2, "khaled", DecisionKind.Approve, "معتمد.", 3));
        Add(4, "ERP-111", "reem", ApprovalAction.Close, [Stage(1, "noura"), Stage(2, "khaled")], 9,
            (1, "noura", DecisionKind.Reject, "التوثيق لا يغطي رموز الأخطاء.", 8));

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
}
