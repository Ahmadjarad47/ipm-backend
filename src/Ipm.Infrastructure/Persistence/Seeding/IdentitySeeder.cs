using Ipm.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ipm.Infrastructure.Persistence.Seeding;

/// <summary>
/// Spec 01 §7. Always: the seven built-in roles and one administrator (from <c>Seed:AdminUsername</c> /
/// <c>Seed:AdminEmail</c> / <c>Seed:AdminPassword</c>), who must change the first password. The <c>system_admin</c> role is
/// kept complete on every start. Development only (<c>Seed:Demo</c>): the front end demo users, all with
/// <c>Seed:DemoPassword</c>. Idempotent: it only adds what is missing.
/// </summary>
public static class IdentitySeeder
{
    /// <summary>Fixed ids of the built-in roles (front end ids in comments).</summary>
    public static class RoleIds
    {
        public static readonly Guid SystemAdmin = new("01970000-0000-7000-8000-000000000001");     // r-admin
        public static readonly Guid Pmo = new("01970000-0000-7000-8000-000000000002");             // r-pmo
        public static readonly Guid Staff = new("01970000-0000-7000-8000-000000000003");           // r-staff
        public static readonly Guid ProjectManager = new("01970000-0000-7000-8000-000000000004");  // r-pm
        public static readonly Guid Approver = new("01970000-0000-7000-8000-000000000005");        // r-approver
        public static readonly Guid TeamMember = new("01970000-0000-7000-8000-000000000006");      // r-member
        public static readonly Guid Viewer = new("01970000-0000-7000-8000-000000000007");          // r-viewer
    }

    private static readonly Guid AdminId = new("01970000-0000-7000-8002-000000000001"); // u-1

    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var demo = configuration.GetValue<bool>("Seed:Demo");

        await SeedRolesAsync(db, cancellationToken);
        await SeedAdministratorAsync(db, users, configuration, demo, cancellationToken);

        if (demo)
        {
            await SeedDemoUsersAsync(db, users, Required(configuration, "Seed:DemoPassword"), cancellationToken);
        }
    }

    private static async Task SeedRolesAsync(ApplicationDbContext db, CancellationToken cancellationToken)
    {
        string[] projectManager =
        [
            Permissions.ProjectsView, Permissions.ProjectsEdit, Permissions.ProjectsArchive, Permissions.ProjectsMembersManage,
            Permissions.TasksView, Permissions.TasksCreate, Permissions.TasksEdit, Permissions.TasksAssign, Permissions.TasksDelete,
            Permissions.WorkflowTransition, Permissions.ApprovalsRequest, Permissions.ApprovalsView, Permissions.ApprovalsDecide,
            Permissions.CommentsCreate, Permissions.CommentsEditOwn, Permissions.FilesUpload, Permissions.FilesDownload, Permissions.FilesDelete,
            Permissions.GitView, Permissions.GitSync, Permissions.DashboardView, Permissions.ReportsView, Permissions.ReportsExport,
            Permissions.WorkItemTypesView, Permissions.ProjectWorkItemTypesConfigure,
            Permissions.WorkItemRelationsView, Permissions.WorkItemRelationsCreate, Permissions.WorkItemRelationsDelete,
        ];

        string[] teamMember =
        [
            Permissions.ProjectsView, Permissions.TasksView, Permissions.TasksCreate, Permissions.TasksEdit, Permissions.WorkflowTransition,
            Permissions.ApprovalsRequest, Permissions.ApprovalsView, Permissions.CommentsCreate, Permissions.CommentsEditOwn,
            Permissions.FilesUpload, Permissions.FilesDownload, Permissions.GitView, Permissions.DashboardView, Permissions.WorkItemTypesView,
            Permissions.WorkItemRelationsView, Permissions.WorkItemRelationsCreate,
        ];

        string[] approver =
        [
            Permissions.ProjectsView, Permissions.TasksView, Permissions.ApprovalsView, Permissions.ApprovalsDecide, Permissions.CommentsCreate,
            Permissions.FilesDownload, Permissions.GitView, Permissions.DashboardView, Permissions.ReportsView, Permissions.WorkItemRelationsView,
        ];

        string[] viewer = [Permissions.ProjectsView, Permissions.TasksView, Permissions.DashboardView, Permissions.WorkItemRelationsView];

        Role[] roles =
        [
            Role.CreateBuiltIn(RoleIds.SystemAdmin, "system_admin", "مدير النظام", "الإعدادات العامة والأدوار وربط Git والتدقيق.", RoleScope.System, Permissions.All),
            Role.CreateBuiltIn(RoleIds.Pmo, "pmo", "مكتب إدارة المشاريع", "إنشاء المشاريع ومتابعة لوحة المؤشرات.", RoleScope.System,
                [Permissions.ProjectsCreate, Permissions.DashboardView, Permissions.ReportsView, Permissions.WorkItemTypesView]),
            Role.CreateBuiltIn(RoleIds.Staff, "staff", "موظف", "الوصول الأساسي للوحة القيادة.", RoleScope.System, [Permissions.DashboardView]),
            Role.CreateBuiltIn(RoleIds.ProjectManager, "project_manager", "مدير مشروع", "إدارة المشروع والأعضاء والمهام ورفع طلبات الموافقة.", RoleScope.Project, projectManager),
            Role.CreateBuiltIn(RoleIds.Approver, "approver", "مشرف / معتمد", "مراجعة الطلبات في المرحلة المخصصة وإصدار القرار.", RoleScope.Project, approver),
            Role.CreateBuiltIn(RoleIds.TeamMember, "team_member", "عضو فريق", "تنفيذ المهام وتحديث الحقول والتعليق.", RoleScope.Project, teamMember),
            Role.CreateBuiltIn(RoleIds.Viewer, "viewer", "قارئ", "مشاهدة الموارد المخصصة فقط.", RoleScope.Project, viewer),
        ];

        var existing = await db.Roles.Select(r => r.Id).ToListAsync(cancellationToken);
        db.Roles.AddRange(roles.Where(r => !existing.Contains(r.Id)));

        // A permission added in code reaches the administrators at the next start.
        var admin = await db.Roles.SingleOrDefaultAsync(r => r.Id == RoleIds.SystemAdmin, cancellationToken);
        admin?.GrantMissing();

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedAdministratorAsync(
        ApplicationDbContext db, UserManager<User> users, IConfiguration configuration, bool demo, CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(u => u.RoleIds.Contains(RoleIds.SystemAdmin), cancellationToken))
        {
            return;
        }

        var username = configuration["Seed:AdminUsername"] is { Length: > 0 } name ? name : "admin";
        var email = Required(configuration, "Seed:AdminEmail");
        var password = Required(configuration, "Seed:AdminPassword");

        var admin = NewUser(users, username, demo ? "سارة العتيبي" : "مدير النظام", email, "مدير النظام", null, [RoleIds.SystemAdmin], AdminId);
        await CreateAsync(users, admin, password);

        // Outside demo the first password came from a configuration file: it must be changed.
        if (!demo)
        {
            admin.PasswordSetByAdministrator();
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task SeedDemoUsersAsync(ApplicationDbContext db, UserManager<User> users, string password, CancellationToken cancellationToken)
    {
        // The administrator (u-1) is seeded above; these are the other users of the front end demo data.
        (string Key, string Username, string Name, string Title, string Email, Guid[] Roles, string? Manager, bool Active)[] demo =
        [
            ("2", "khaled", "خالد الحربي", "مدير مشروع أول", "khaled@example.local", [RoleIds.Pmo, RoleIds.Staff], "1", true),
            ("3", "noura", "نورة القحطاني", "مشرفة تقنية", "noura@example.local", [RoleIds.Staff], "2", true),
            ("4", "fahad", "فهد الدوسري", "مطوّر واجهات", "fahad@example.local", [RoleIds.Staff], "3", true),
            ("5", "reem", "ريم الزهراني", "محللة أعمال", "reem@example.local", [RoleIds.Staff], "3", true),
            ("6", "abdullah", "عبدالله الشهري", "مراجع جودة", "abdullah@example.local", [RoleIds.Staff], "3", true),
            ("7", "mona", "منى السبيعي", "مطوّرة خلفية", "mona@example.local", [RoleIds.Staff], null, false),
        ];

        var existing = await db.Users.Select(u => u.Id).ToListAsync(cancellationToken);
        foreach (var d in demo)
        {
            var id = DemoId(d.Key);
            if (existing.Contains(id))
            {
                continue;
            }

            var user = NewUser(users, d.Username, d.Name, d.Email, d.Title, d.Manager is null ? null : DemoId(d.Manager), d.Roles, id);
            await CreateAsync(users, user, password);

            if (!d.Active)
            {
                user.Deactivate(null);
                await db.SaveChangesAsync(cancellationToken);
            }
        }
    }

    private static User NewUser(UserManager<User> users, string username, string name, string email, string title, Guid? managerId, Guid[] roleIds, Guid id) =>
        User.Create(
            username, users.NormalizeName(username.ToLowerInvariant()), name, email, users.NormalizeEmail(email), title, managerId, roleIds, id);

    private static async Task CreateAsync(UserManager<User> users, User user, string password)
    {
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Seeding user {user.UserName} failed: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }
    }

    private static Guid DemoId(string key) => new($"01970000-0000-7000-8002-00000000000{key}");

    private static string Required(IConfiguration configuration, string key) =>
        configuration[key] is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Configuration '{key}' is required to seed the database.");
}
