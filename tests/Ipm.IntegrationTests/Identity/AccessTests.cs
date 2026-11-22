using System.Net;
using System.Net.Http.Json;
using Ipm.Domain.Identity;
using Ipm.Infrastructure.Persistence;
using Ipm.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Ipm.IntegrationTests.Identity.IdentityApi;

namespace Ipm.IntegrationTests.Identity;

/// <summary>Scenarios 3, 4, 5, 9 (R9–R13) and 12 of docs/modules/01-identity.md: who may do what.</summary>
[Collection(ApiCollection.Name)]
public sealed class AccessTests(ApiFactory factory)
{
    private static readonly Uri Roles = new("/api/v1/roles", UriKind.Relative);
    private static readonly Uri Users = new("/api/v1/users", UriKind.Relative);

    [Fact]
    public async Task Scenario_3_a_user_without_the_permission_gets_403_and_it_is_recorded()
    {
        using var noura = await factory.SignedInClientAsync("noura", ApiFactory.DemoPassword);

        var response = await noura.PostAsJsonAsync(Roles, new { name = Unique("x"), scope = "system" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var noura1 = await db.Users.Where(u => u.UserName == "noura").Select(u => u.Id).SingleAsync();
        Assert.True(await db.SecurityEvents.AnyAsync(e =>
            e.UserId == noura1 && e.Type == SecurityEventType.PermissionDenied && e.Permission == Permissions.RolesManage));
    }

    [Fact]
    public async Task Scenario_4_removing_a_permission_from_a_role_is_felt_on_the_very_next_request()
    {
        using var admin = await factory.AdminClientAsync();
        var (roleId, etag) = await CreateRoleAsync(admin, Unique("grp"), RoleScope.System, [Permissions.PermissionsAssign, Permissions.DashboardView]);
        var user = await ReadyUserAsync(factory, admin, "rev", roleId);
        Assert.Equal(HttpStatusCode.OK, (await user.Client.GetAsync(Users)).StatusCode);

        var edit = await SendAsync(
            admin, HttpMethod.Put, $"/api/v1/roles/{roleId}",
            new { name = Unique("grp"), description = "x", permissions = new[] { Permissions.DashboardView }, denied = Array.Empty<string>() }, etag);
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await user.Client.GetAsync(Users)).StatusCode);
    }

    [Fact]
    public async Task Scenario_5_an_explicit_deny_in_one_role_beats_a_grant_in_another()
    {
        using var admin = await factory.AdminClientAsync();
        var (granting, _) = await CreateRoleAsync(admin, Unique("grant"), RoleScope.System, [Permissions.PermissionsAssign]);
        var (denying, _) = await CreateRoleAsync(admin, Unique("deny"), RoleScope.System, denied: [Permissions.PermissionsAssign]);
        var user = await ReadyUserAsync(factory, admin, "deny", granting, denying);

        Assert.Equal(HttpStatusCode.Forbidden, (await user.Client.GetAsync(Users)).StatusCode);

        var me = await ReadAsync(await user.Client.GetAsync(new Uri("/api/v1/auth/me", UriKind.Relative)));
        Assert.Contains(Permissions.PermissionsAssign, me.GetProperty("denied").EnumerateArray().Select(p => p.GetString()));
        Assert.DoesNotContain(Permissions.PermissionsAssign, me.GetProperty("systemPermissions").EnumerateArray().Select(p => p.GetString()));
    }

    [Fact]
    public async Task Scenario_9_R9_the_administrator_cannot_deactivate_themselves()
    {
        using var admin = await factory.AdminClientAsync();
        var adminId = await MyIdAsync(admin);
        var (_, etag) = await GetAsync(admin, $"/api/v1/users/{adminId}");

        var response = await SendAsync(admin, HttpMethod.Post, $"/api/v1/users/{adminId}/deactivate", null, etag);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("User.CannotDeactivateSelf", (await ProblemAsync(response)).Code);
    }

    [Fact]
    public async Task Scenario_9_R10_nobody_changes_their_own_roles()
    {
        using var admin = await factory.AdminClientAsync();
        var adminId = await MyIdAsync(admin);
        var (_, etag) = await GetAsync(admin, $"/api/v1/users/{adminId}");

        var response = await SendAsync(admin, HttpMethod.Put, $"/api/v1/users/{adminId}/roles", new { systemRoleIds = new[] { await RoleIdAsync(admin, "staff") } }, etag);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("User.CannotChangeOwnRoles", (await ProblemAsync(response)).Code);
    }

    [Fact]
    public async Task Scenario_9_R11_a_lesser_administrator_cannot_act_on_a_more_powerful_account()
    {
        using var admin = await factory.AdminClientAsync();
        var adminId = await MyIdAsync(admin);
        var (lesserRole, _) = await CreateRoleAsync(admin, Unique("lesser"), RoleScope.System, [Permissions.PermissionsAssign]);
        var lesser = await ReadyUserAsync(factory, admin, "lesser", lesserRole);
        var (_, etag) = await GetAsync(admin, $"/api/v1/users/{adminId}");

        var deactivate = await SendAsync(lesser.Client, HttpMethod.Post, $"/api/v1/users/{adminId}/deactivate", null, etag);
        var reset = await SendAsync(lesser.Client, HttpMethod.Post, $"/api/v1/users/{adminId}/reset-password", new { newPassword = "Hijack12345" }, null);

        Assert.Equal(HttpStatusCode.Forbidden, deactivate.StatusCode);
        Assert.Equal("User.TargetOutranksCaller", (await ProblemAsync(deactivate)).Code);
        Assert.Equal(HttpStatusCode.Forbidden, reset.StatusCode);
        Assert.Equal("User.TargetOutranksCaller", (await ProblemAsync(reset)).Code);
    }

    [Fact]
    public async Task Scenario_9_R12_you_cannot_hand_out_a_permission_you_do_not_hold()
    {
        using var admin = await factory.AdminClientAsync();
        var (lesserRole, _) = await CreateRoleAsync(admin, Unique("assigner"), RoleScope.System, [Permissions.PermissionsAssign]);
        var lesser = await ReadyUserAsync(factory, admin, "assigner", lesserRole);
        var (targetId, etag) = await CreateUserAsync(admin, Unique("target"));

        // Assigning a role that carries permissions the caller lacks.
        var assign = await SendAsync(
            lesser.Client, HttpMethod.Put, $"/api/v1/users/{targetId}/roles", new { systemRoleIds = new[] { await RoleIdAsync(admin, "pmo") } }, etag);
        Assert.Equal(HttpStatusCode.Forbidden, assign.StatusCode);
        Assert.Equal("Role.PermissionNotGrantable", (await ProblemAsync(assign)).Code);

        // Adding a permission to a role.
        var (managerRole, _) = await CreateRoleAsync(admin, Unique("manager"), RoleScope.System, [Permissions.RolesManage]);
        var manager = await ReadyUserAsync(factory, admin, "manager", managerRole);
        var create = await manager.Client.PostAsJsonAsync(
            Roles, new { name = Unique("big"), scope = "system", permissions = new[] { Permissions.ProjectsCreate } });
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal("Role.PermissionNotGrantable", (await ProblemAsync(create)).Code);
    }

    [Fact]
    public async Task Scenario_9_R13_the_last_role_manager_cannot_lose_the_permission()
    {
        using var admin = await factory.AdminClientAsync();
        var (role, etag) = await GetAsync(admin, $"/api/v1/roles/{await RoleIdAsync(admin, "system_admin")}");
        var kept = role.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()!).Where(p => p != Permissions.RolesManage).ToArray();

        var response = await SendAsync(
            admin, HttpMethod.Put, $"/api/v1/roles/{role.GetProperty("id").GetGuid()}",
            new { name = role.GetProperty("name").GetString(), description = "", permissions = kept, denied = Array.Empty<string>() }, etag);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("User.LastRoleManager", (await ProblemAsync(response)).Code);
    }

    [Fact]
    public async Task Scenario_9_R19_project_roles_cannot_be_given_to_a_user_directly()
    {
        using var admin = await factory.AdminClientAsync();
        var (targetId, etag) = await CreateUserAsync(admin, Unique("proj"));

        var response = await SendAsync(
            admin, HttpMethod.Put, $"/api/v1/users/{targetId}/roles", new { systemRoleIds = new[] { await RoleIdAsync(admin, "project_manager") } }, etag);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("User.RoleScopeInvalid", (await ProblemAsync(response)).Code);
    }

    [Fact]
    public async Task Scenario_12_the_directory_hides_email_manager_and_roles_from_ordinary_users()
    {
        using var noura = await factory.SignedInClientAsync("noura", ApiFactory.DemoPassword);
        using var admin = await factory.AdminClientAsync();

        var directory = await ReadAsync(await noura.GetAsync(new Uri("/api/v1/users/directory", UriKind.Relative)));
        Assert.True(directory.GetArrayLength() >= 7);
        Assert.All(directory.EnumerateArray(), item =>
        {
            Assert.False(item.TryGetProperty("email", out _));
            Assert.False(item.TryGetProperty("systemRoleIds", out _));
            Assert.False(item.TryGetProperty("managerId", out _));
        });

        Assert.Equal(HttpStatusCode.Forbidden, (await noura.GetAsync(Users)).StatusCode);

        var khaledId = directory.EnumerateArray().Single(u => u.GetProperty("username").GetString() == "khaled").GetProperty("id").GetGuid();
        var asNoura = await ReadAsync(await noura.GetAsync(new Uri($"/api/v1/users/{khaledId}", UriKind.Relative)));
        Assert.Equal(string.Empty, asNoura.GetProperty("email").GetString());
        Assert.Empty(asNoura.GetProperty("systemRoleIds").EnumerateArray());

        var asAdmin = await ReadAsync(await admin.GetAsync(new Uri($"/api/v1/users/{khaledId}", UriKind.Relative)));
        Assert.Equal("khaled@example.local", asAdmin.GetProperty("email").GetString());
        Assert.Equal(2, asAdmin.GetProperty("systemRoleIds").GetArrayLength());
    }

    private static async Task<Guid> MyIdAsync(HttpClient client) =>
        (await ReadAsync(await client.GetAsync(new Uri("/api/v1/auth/me", UriKind.Relative)))).GetProperty("user").GetProperty("id").GetGuid();
}
