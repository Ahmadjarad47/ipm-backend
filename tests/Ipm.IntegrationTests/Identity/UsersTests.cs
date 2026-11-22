using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ipm.Domain.Identity;
using Ipm.Infrastructure.Persistence;
using Ipm.Infrastructure.Persistence.Auditing;
using Ipm.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Ipm.IntegrationTests.Identity.IdentityApi;

namespace Ipm.IntegrationTests.Identity;

/// <summary>Scenarios 9 (R7, R14), 10, 13 and 14 of docs/modules/01-identity.md.</summary>
[Collection(ApiCollection.Name)]
public sealed class UsersTests(ApiFactory factory)
{
    [Fact]
    public async Task Scenario_9_R7_username_and_email_are_unique()
    {
        using var admin = await factory.AdminClientAsync();
        var username = Unique("dup");
        await CreateUserAsync(admin, username);

        var sameName = await admin.PostAsJsonAsync(
            new Uri("/api/v1/users", UriKind.Relative),
            new { username = username.ToUpperInvariant().ToLowerInvariant(), name = "x", email = $"other-{username}@test.example", systemRoleIds = Array.Empty<Guid>(), password = FirstPassword });
        Assert.Equal(HttpStatusCode.Conflict, sameName.StatusCode);
        Assert.Equal("User.UsernameTaken", (await ProblemAsync(sameName)).Code);

        var sameEmail = await admin.PostAsJsonAsync(
            new Uri("/api/v1/users", UriKind.Relative),
            new { username = Unique("other"), name = "x", email = $"{username.ToUpperInvariant()}@TEST.EXAMPLE", systemRoleIds = Array.Empty<Guid>(), password = FirstPassword });
        Assert.Equal(HttpStatusCode.Conflict, sameEmail.StatusCode);
        Assert.Equal("User.EmailTaken", (await ProblemAsync(sameEmail)).Code);
    }

    [Fact]
    public async Task Scenario_10_R14_the_manager_must_be_an_active_user_and_never_close_a_loop()
    {
        using var admin = await factory.AdminClientAsync();
        var (a, aTag) = await CreateUserAsync(admin, Unique("lead"));
        var (b, bTag) = await CreateUserAsync(admin, Unique("report"), managerId: a);

        // Inactive manager (mona is seeded as inactive).
        var directory = await ReadAsync(await admin.GetAsync(new Uri("/api/v1/users/directory", UriKind.Relative)));
        var mona = directory.EnumerateArray().Single(u => u.GetProperty("username").GetString() == "mona").GetProperty("id").GetGuid();
        var inactive = await SendAsync(admin, HttpMethod.Put, $"/api/v1/users/{b}", Profile(b, mona), bTag);
        Assert.Equal(HttpStatusCode.BadRequest, inactive.StatusCode);
        Assert.Equal("User.ManagerInvalid", (await ProblemAsync(inactive)).Code);

        // Themselves.
        var self = await SendAsync(admin, HttpMethod.Put, $"/api/v1/users/{a}", Profile(a, a), aTag);
        Assert.Equal(HttpStatusCode.BadRequest, self.StatusCode);

        // A loop: b already reports to a, so a cannot report to b.
        var loop = await SendAsync(admin, HttpMethod.Put, $"/api/v1/users/{a}", Profile(a, b), aTag);
        Assert.Equal(HttpStatusCode.BadRequest, loop.StatusCode);
        Assert.Equal("User.ManagerInvalid", (await ProblemAsync(loop)).Code);
    }

    [Fact]
    public async Task Scenario_13_a_user_edit_with_a_stale_etag_is_412_and_without_if_match_428()
    {
        using var admin = await factory.AdminClientAsync();
        var (id, etag) = await CreateUserAsync(admin, Unique("etag"));

        var first = await SendAsync(admin, HttpMethod.Put, $"/api/v1/users/{id}", Profile(id, null, "الأول"), etag);
        var second = await SendAsync(admin, HttpMethod.Put, $"/api/v1/users/{id}", Profile(id, null, "الثاني"), etag);
        var missing = await SendAsync(admin, HttpMethod.Put, $"/api/v1/users/{id}", Profile(id, null, "الثالث"), null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.NotEqual(etag, first.Headers.ETag);
        Assert.Equal(HttpStatusCode.PreconditionFailed, second.StatusCode);
        Assert.Equal("Concurrency.StaleVersion", (await ProblemAsync(second)).Code);
        Assert.Equal(HttpStatusCode.PreconditionRequired, missing.StatusCode);
    }

    [Fact]
    public async Task Scenario_14_creating_a_user_is_audited_without_any_secret()
    {
        using var admin = await factory.AdminClientAsync();
        var adminId = (await ReadAsync(await admin.GetAsync(new Uri("/api/v1/auth/me", UriKind.Relative)))).GetProperty("user").GetProperty("id").GetGuid();
        var (id, _) = await CreateUserAsync(admin, Unique("audit"));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var row = await db.Set<AuditLogEntry>().SingleAsync(e => e.EntityId == id && e.Action == AuditAction.Insert);

        Assert.Equal("identity.users", row.Entity);
        Assert.Equal(adminId, row.UserId);
        using var after = JsonDocument.Parse(row.After!);
        Assert.True(after.RootElement.TryGetProperty("userName", out _));
        Assert.False(after.RootElement.TryGetProperty("passwordHash", out _));
        Assert.False(after.RootElement.TryGetProperty("securityStamp", out _));
        Assert.False(after.RootElement.TryGetProperty("version", out _));
    }

    [Fact]
    public async Task Changing_roles_is_audited_and_changes_the_etag()
    {
        using var admin = await factory.AdminClientAsync();
        var staff = await RoleIdAsync(admin, "staff");
        var (id, etag) = await CreateUserAsync(admin, Unique("roles"));

        var response = await SendAsync(admin, HttpMethod.Put, $"/api/v1/users/{id}/roles", new { systemRoleIds = new[] { staff } }, etag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(etag, response.Headers.ETag);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var update = await db.Set<AuditLogEntry>().Where(e => e.EntityId == id && e.Action == AuditAction.Update).OrderBy(e => e.Id).LastAsync();
        Assert.Contains("roleIds", update.After, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_list_searches_name_username_and_email_and_filters_by_state()
    {
        using var admin = await factory.AdminClientAsync();

        var byName = await ReadAsync(await admin.GetAsync(new Uri("/api/v1/users?q=خالد", UriKind.Relative)));
        Assert.Equal("khaled", byName.GetProperty("items")[0].GetProperty("username").GetString());

        var byEmail = await ReadAsync(await admin.GetAsync(new Uri("/api/v1/users?q=NOURA@example", UriKind.Relative)));
        Assert.Equal(1, byEmail.GetProperty("total").GetInt32());

        var inactive = await ReadAsync(await admin.GetAsync(new Uri("/api/v1/users?active=false", UriKind.Relative)));
        Assert.Contains("mona", inactive.GetProperty("items").EnumerateArray().Select(u => u.GetProperty("username").GetString()));
        Assert.All(inactive.GetProperty("items").EnumerateArray(), u => Assert.False(u.GetProperty("isActive").GetBoolean()));
    }

    [Fact]
    public async Task Deactivated_users_can_be_activated_again()
    {
        using var admin = await factory.AdminClientAsync();
        var (id, etag) = await CreateUserAsync(admin, Unique("again"));
        var off = await SendAsync(admin, HttpMethod.Post, $"/api/v1/users/{id}/deactivate", null, etag);
        Assert.Equal(HttpStatusCode.OK, off.StatusCode);

        var on = await SendAsync(admin, HttpMethod.Post, $"/api/v1/users/{id}/activate", null, off.Headers.ETag);

        Assert.Equal(HttpStatusCode.OK, on.StatusCode);
        Assert.True((await ReadAsync(on)).GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task An_administrator_reset_ends_the_sessions_clears_the_lockout_and_requires_a_new_password()
    {
        using var admin = await factory.AdminClientAsync();
        var staff = await RoleIdAsync(admin, "staff");
        var user = await ReadyUserAsync(factory, admin, "reset", staff);
        using var other = factory.CreateClient();
        for (var i = 0; i < 5; i++)
        {
            await LoginAsync(other, user.Username, "Wrong12345");
        }

        var reset = await SendAsync(admin, HttpMethod.Post, $"/api/v1/users/{user.Id}/reset-password", new { newPassword = "Temp12345" }, null);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await user.Client.GetAsync(new Uri("/api/v1/auth/me", UriKind.Relative))).StatusCode);
        var login = await LoginAsync(other, user.Username, "Temp12345");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.True((await login.Content.ReadFromJsonAsync<SessionDto>())!.MustChangePassword);
    }

    private static object Profile(Guid id, Guid? managerId, string name = "اسم") =>
        new { name, email = $"profile-{id:N}@test.example", title = "مسمى", managerId };
}
