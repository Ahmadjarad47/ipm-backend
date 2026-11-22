using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Ipm.Domain.Identity;
using Ipm.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ipm.Infrastructure.Persistence.Seeding;

/// <summary>
/// Spec 04 §6. Always: the two default project templates. Development only (<c>Seed:Demo</c>): the four demo projects of the front
/// end (ERP, SSP, BI and the archived MAIL) with their members, roles and allowed types. Idempotent: it adds only what is missing
/// (templates by name, projects by code) and never touches one that exists. The demo projects are written in their final status
/// directly, without an approval, because they are sample data.
/// </summary>
public static class ProjectsSeeder
{
    private static Guid TemplateId(int number) => new($"01970000-0000-7000-8008-{number:D12}");

    private static Guid ProjectId(int number) => new($"01970000-0000-7000-8007-{number:D12}");

    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var lookup = await Lookup.LoadAsync(db, cancellationToken);
        await SeedTemplatesAsync(db, lookup, cancellationToken);

        if (configuration.GetValue<bool>("Seed:Demo"))
        {
            await SeedDemoProjectsAsync(db, lookup, cancellationToken);
        }
    }

    private static async Task SeedTemplatesAsync(ApplicationDbContext db, Lookup lookup, CancellationToken cancellationToken)
    {
        var defaults = new (int Number, string Name, string Description, string[] Types, int Days, string[] Roles)[]
        {
            (1, "مشروع تطوير برمجي", "ميزات ومهام وأخطاء ومعالم، يبدأ قيد الإعداد ويحتاج اعتماداً للتفعيل.",
                ["feature", "task", "bug", "subtask", "milestone"], 120, ["project_manager", "approver", "team_member"]),
            (2, "مشروع تشغيلي", "مهام وأخطاء فقط لمشاريع التشغيل والدعم القصيرة.", ["task", "bug"], 45, ["project_manager", "team_member"]),
        };

        var existing = await db.Set<ProjectTemplate>().Select(t => t.Name).ToListAsync(cancellationToken);
        foreach (var d in defaults.Where(d => !existing.Contains(d.Name)))
        {
            var template = ProjectTemplate.Create(
                d.Name, d.Description, true, d.Types.Select(lookup.Type), lookup.Type("task"), lookup.InitialProjectStatus,
                d.Roles.Select(lookup.Role), d.Days, [], TemplateId(d.Number)).Value;
            db.Add(template);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedDemoProjectsAsync(ApplicationDbContext db, Lookup lookup, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        string[] all = ["feature", "task", "bug", "subtask", "milestone"];

        var demo = new (int Number, string Code, string Name, string Description, string Owner, string Status, int Start, int End, bool Archived,
            (string User, string Role)[] Members, string[] Types, string DefaultType)[]
        {
            (1, "ERP", "تطوير نظام ERP الداخلي", "إعادة بناء وحدات المشتريات والمخزون بواجهة عربية موحدة وربطها بالخدمات المركزية.",
                "khaled", "active", -60, 75, false,
                [("khaled", "project_manager"), ("noura", "approver"), ("fahad", "team_member"), ("reem", "team_member"), ("abdullah", "viewer")],
                all, "task"),
            (2, "SSP", "بوابة الخدمات الذاتية", "بوابة للموظفين لتقديم الطلبات الإدارية ومتابعتها إلكترونياً.",
                "khaled", "active", -35, 40, false,
                [("khaled", "project_manager"), ("noura", "approver"), ("reem", "team_member")],
                ["feature", "task", "bug"], "task"),
            (3, "BI", "منصة البيانات والتقارير", "مستودع بيانات موحد ولوحات مؤشرات للإدارة العليا.",
                "noura", "pending", 10, 150, false,
                [("noura", "project_manager"), ("fahad", "team_member"), ("khaled", "approver")],
                ["feature", "task", "milestone"], "feature"),
            (4, "MAIL", "ترحيل البريد المؤسسي", "ترحيل صناديق البريد إلى المنصة الجديدة.",
                "khaled", "completed", -160, -20, true,
                [("khaled", "project_manager"), ("fahad", "team_member")],
                ["task", "bug"], "task"),
        };

        var users = await db.Users.Select(u => new { u.Id, u.UserName }).ToDictionaryAsync(u => u.UserName!, u => u.Id, cancellationToken);
        var existing = await db.Set<Project>().Select(p => p.Code).ToListAsync(cancellationToken);

        foreach (var d in demo.Where(d => !existing.Contains(d.Code) && d.Members.All(m => users.ContainsKey(m.User))))
        {
            var project = Project.Create(
                d.Code, d.Name, d.Description, users[d.Owner], lookup.ProjectStatus(d.Status),
                today.AddDays(d.Start), today.AddDays(d.End), d.Types.Select(lookup.Type), lookup.Type(d.DefaultType), ProjectId(d.Number)).Value;
            project.SetMembers([.. d.Members.Select(m => (users[m.User], lookup.Role(m.Role)))]).ThrowIfFailure();
            if (d.Archived)
            {
                project.Archive();
            }

            db.Add(project);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The ids the seed needs, looked up by key so it works whatever the ids of an edited database are.</summary>
    private sealed class Lookup
    {
        private Dictionary<string, Guid> _types = [];
        private Dictionary<string, Guid> _roles = [];
        private Dictionary<string, Guid> _projectStatuses = [];

        public Guid InitialProjectStatus { get; private set; }

        public Guid Type(string key) => _types[key];

        public Guid Role(string key) => _roles[key];

        public Guid ProjectStatus(string key) => _projectStatuses[key];

        public static async Task<Lookup> LoadAsync(ApplicationDbContext db, CancellationToken cancellationToken)
        {
            var workflow = await db.Set<Workflow>().SingleAsync(w => w.Key == "project_default" && w.Scope == StatusScope.Project, cancellationToken);
            return new Lookup
            {
                InitialProjectStatus = workflow.InitialStatusId,
                _types = await db.Set<WorkItemType>().ToDictionaryAsync(t => t.Key, t => t.Id, cancellationToken),
                _roles = await db.Set<Role>().ToDictionaryAsync(r => r.Key, r => r.Id, cancellationToken),
                _projectStatuses = await db.Set<Status>().Where(s => s.Scope == StatusScope.Project).ToDictionaryAsync(s => s.Key, s => s.Id, cancellationToken),
            };
        }
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
