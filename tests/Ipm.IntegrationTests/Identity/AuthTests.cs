using System.Net;
using System.Net.Http.Json;
using Ipm.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ipm.Infrastructure.Persistence;
using Ipm.Domain.Identity;
using static Ipm.IntegrationTests.Identity.IdentityApi;

namespace Ipm.IntegrationTests.Identity;

/// <summary>Scenarios 1, 2, 6, 7 and 8 of docs/modules/01-identity.md: signing in, sessions, lockout.</summary>
[Collection(ApiCollection.Name)]
public sealed class AuthTests(ApiFactory factory)
{
    [Fact]
    public async Task Scenario_1_a_temporary_password_must_be_changed_before_anything_else()
    {
        using var admin = await factory.AdminClientAsync();
        var username = Unique("first");
        await CreateUserAsync(admin, username, [await RoleIdAsync(admin, "staff")]);

        using var client = factory.CreateClient();
        var login = await LoginAsync(client, username, FirstPassword);
        var session = await login.Content.ReadFromJsonAsync<SessionDto>();
        Assert.True(session!.MustChangePassword);
        client.WithToken(session.AccessToken);

        var blocked = await client.GetAsync(new Uri("/api/v1/users/directory", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal("Auth.PasswordChangeRequired", (await ProblemAsync(blocked)).Code);

        var me = await ReadAsync(await client.GetAsync(new Uri("/api/v1/auth/me", UriKind.Relative)));
        Assert.True(me.GetProperty("mustChangePassword").GetBoolean());

        var change = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/password", UriKind.Relative), new { currentPassword = FirstPassword, newPassword = OwnPassword });
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
        var renewed = await change.Content.ReadFromJsonAsync<SessionDto>();
        Assert.False(renewed!.MustChangePassword);

        client.WithToken(renewed.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(new Uri("/api/v1/users/directory", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task Scenario_1_the_administrator_holds_all_42_permissions()
    {
        using var admin = await factory.AdminClientAsync();

        var me = await ReadAsync(await admin.GetAsync(new Uri("/api/v1/auth/me", UriKind.Relative)));

        Assert.Equal(42, me.GetProperty("systemPermissions").GetArrayLength());
        Assert.Equal("admin", me.GetProperty("user").GetProperty("username").GetString());
        Assert.False(me.GetProperty("mustChangePassword").GetBoolean());
    }

    [Fact]
    public async Task Scenario_2_everything_but_login_and_refresh_needs_a_token()
    {
        using var client = factory.CreateClient();

        foreach (var url in new[] { "/api/v1/roles", "/api/v1/users/directory", "/api/v1/auth/me", "/api/v1/permissions" })
        {
            var response = await client.GetAsync(new Uri(url, UriKind.Relative));
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var noBody = await client.PostAsJsonAsync(new Uri("/api/v1/auth/login", UriKind.Relative), new { username = "", password = "" });
        Assert.Equal(HttpStatusCode.BadRequest, noBody.StatusCode);

        var refresh = await client.PostAsync(new Uri("/api/v1/auth/refresh", UriKind.Relative), content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        Assert.Equal("Auth.RefreshInvalid", (await ProblemAsync(refresh)).Code);
    }

    [Fact]
    public async Task Scenario_6_five_wrong_passwords_lock_the_account_even_for_the_right_one()
    {
        using var admin = await factory.AdminClientAsync();
        var username = Unique("lock");
        await CreateUserAsync(admin, username);
        using var client = factory.CreateClient();

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var wrong = await LoginAsync(client, username, "Wrong12345");
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
            Assert.Equal("Auth.InvalidCredentials", (await ProblemAsync(wrong)).Code);
        }

        var fifth = await LoginAsync(client, username, "Wrong12345");
        Assert.Equal(HttpStatusCode.TooManyRequests, fifth.StatusCode);
        Assert.Equal("Auth.LockedOut", (await ProblemAsync(fifth)).Code);

        var right = await LoginAsync(client, username, FirstPassword);
        Assert.Equal(HttpStatusCode.TooManyRequests, right.StatusCode);
        Assert.Equal("Auth.LockedOut", (await ProblemAsync(right)).Code);
    }

    [Fact]
    public async Task Scenario_6_an_unknown_name_gets_the_same_answer_as_a_wrong_password()
    {
        using var client = factory.CreateClient();

        var unknown = await ProblemAsync(await LoginAsync(client, "nobody-" + Guid.NewGuid().ToString("N")[..8], "Whatever123"));
        var wrong = await ProblemAsync(await LoginAsync(client, "noura", "Whatever123"));

        Assert.Equal(wrong.Code, unknown.Code);
        Assert.Equal(wrong.Root.GetProperty("detail").GetString(), unknown.Root.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Scenario_6_failed_and_successful_sign_ins_are_recorded_as_security_events()
    {
        using var admin = await factory.AdminClientAsync();
        var username = Unique("event");
        var (id, _) = await CreateUserAsync(admin, username);
        using var client = factory.CreateClient();
        await LoginAsync(client, username, "Wrong12345");
        await LoginAsync(client, username, FirstPassword);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var events = await db.SecurityEvents.Where(e => e.UserId == id).OrderBy(e => e.Id).ToListAsync();

        Assert.Equal([SecurityEventType.LoginFailed, SecurityEventType.LoginSucceeded], events.Select(e => e.Type));
        Assert.Equal("wrong-password", events[0].Reason);
    }

    [Fact]
    public async Task Scenario_7_refresh_rotates_the_token_and_reusing_an_old_one_ends_every_session()
    {
        using var admin = await factory.AdminClientAsync();
        var username = Unique("refresh");
        await CreateUserAsync(admin, username);
        using var client = factory.CreateClient();

        var first = RefreshCookie(await LoginAsync(client, username, FirstPassword));
        Assert.NotNull(first);

        var rotated = await RefreshAsync(client, first);
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        var second = RefreshCookie(rotated);
        Assert.NotNull(second);
        Assert.NotEqual(first, second);

        var reuse = await RefreshAsync(client, first);
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        Assert.Equal("Auth.RefreshInvalid", (await ProblemAsync(reuse)).Code);

        var afterTheft = await RefreshAsync(client, second);
        Assert.Equal(HttpStatusCode.Unauthorized, afterTheft.StatusCode);
    }

    [Fact]
    public async Task Scenario_7_logout_revokes_the_refresh_token()
    {
        using var admin = await factory.AdminClientAsync();
        var username = Unique("logout");
        await CreateUserAsync(admin, username);
        using var client = factory.CreateClient();
        var login = await LoginAsync(client, username, FirstPassword);
        var cookie = RefreshCookie(login)!;
        client.WithToken((await login.Content.ReadFromJsonAsync<SessionDto>())!.AccessToken);

        using var logout = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        logout.Headers.Add("Cookie", cookie);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(logout)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(client, cookie)).StatusCode);
    }

    [Fact]
    public async Task Scenario_8_a_deactivated_user_loses_every_session_at_once()
    {
        using var admin = await factory.AdminClientAsync();
        var staff = await RoleIdAsync(admin, "staff");
        var user = await ReadyUserAsync(factory, admin, "gone", staff);
        using var client = factory.CreateClient();
        var cookie = RefreshCookie(await LoginAsync(client, user.Username, OwnPassword))!;
        Assert.Equal(HttpStatusCode.OK, (await user.Client.GetAsync(new Uri("/api/v1/auth/me", UriKind.Relative))).StatusCode);

        var (_, etag) = await GetAsync(admin, $"/api/v1/users/{user.Id}");
        var deactivate = await SendAsync(admin, HttpMethod.Post, $"/api/v1/users/{user.Id}/deactivate", null, etag);
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        Assert.False((await ReadAsync(deactivate)).GetProperty("isActive").GetBoolean());

        Assert.Equal(HttpStatusCode.Unauthorized, (await user.Client.GetAsync(new Uri("/api/v1/auth/me", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(client, cookie)).StatusCode);
        var login = await LoginAsync(client, user.Username, OwnPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Equal("Auth.InvalidCredentials", (await ProblemAsync(login)).Code);
    }

    [Fact]
    public async Task Scenario_9_weak_passwords_are_refused_with_an_arabic_message_on_the_field()
    {
        using var admin = await factory.AdminClientAsync();

        var response = await admin.PostAsJsonAsync(
            new Uri("/api/v1/users", UriKind.Relative),
            new { username = Unique("weak"), name = "ضعيف", email = $"{Unique("weak")}@test.example", systemRoleIds = Array.Empty<Guid>(), password = "short" });
        var problem = await ProblemAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var messages = problem.Root.GetProperty("errors").GetProperty("password").EnumerateArray().Select(m => m.GetString()!).ToList();
        Assert.All(messages, m => Assert.Contains(m, c => c is >= '؀' and <= 'ۿ'));
    }

    [Fact]
    public async Task Scenario_9_changing_the_password_needs_the_current_one()
    {
        using var admin = await factory.AdminClientAsync();
        var user = await ReadyUserAsync(factory, admin, "pwd");

        var wrong = await user.Client.PostAsJsonAsync(
            new Uri("/api/v1/auth/password", UriKind.Relative), new { currentPassword = "Wrong12345", newPassword = "Another12345" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, wrong.StatusCode);
        Assert.Equal("Auth.WrongCurrentPassword", (await ProblemAsync(wrong)).Code);
    }
}
