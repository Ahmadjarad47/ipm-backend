using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Ipm.Domain.Identity;
using Ipm.IntegrationTests.Foundation;
using Ipm.IntegrationTests.Infrastructure;

namespace Ipm.IntegrationTests.Identity;

/// <summary>A user created through the API and signed in with their own password, ready for a test to act as them.</summary>
public sealed record TestUser(Guid Id, string Username, HttpClient Client);

/// <summary>Helpers that wrap the identity HTTP calls so the scenarios read like the spec.</summary>
internal static class IdentityApi
{
    public const string FirstPassword = "First12345";
    public const string OwnPassword = "Own12345";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string Unique(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 8, 30)];

    public static Task<HttpResponseMessage> LoginAsync(HttpClient client, string username, string password) =>
        client.PostAsJsonAsync(new Uri("/api/v1/auth/login", UriKind.Relative), new { username, password });

    /// <summary>The refresh cookie value of a sign-in or refresh response (the test host serves http, so the Secure cookie is replayed by hand).</summary>
    public static string? RefreshCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.Select(v => v.Split(';')[0]).FirstOrDefault(v => v.StartsWith("ipm_refresh=", StringComparison.Ordinal))
            : null;

    public static async Task<HttpResponseMessage> RefreshAsync(HttpClient client, string cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("Cookie", cookie);
        return await client.SendAsync(request);
    }

    public static HttpClient WithToken(this HttpClient client, string accessToken)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    public static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    public static async Task<Problem> ProblemAsync(HttpResponseMessage response) => await Problem.ReadAsync(response);

    public static async Task<Guid> RoleIdAsync(HttpClient admin, string key)
    {
        var page = await ReadAsync(await admin.GetAsync(new Uri("/api/v1/roles?pageSize=200", UriKind.Relative)));
        return page.GetProperty("items").EnumerateArray().Single(r => r.GetProperty("key").GetString() == key).GetProperty("id").GetGuid();
    }

    public static async Task<(Guid Id, EntityTagHeaderValue ETag)> CreateRoleAsync(
        HttpClient admin, string name, RoleScope scope, string[]? permissions = null, string[]? denied = null)
    {
        var response = await admin.PostAsJsonAsync(
            new Uri("/api/v1/roles", UriKind.Relative),
            new { name, description = "test", scope = scope == RoleScope.System ? "system" : "project", permissions = permissions ?? [], denied = denied ?? [] });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return ((await ReadAsync(response)).GetProperty("id").GetGuid(), response.Headers.ETag!);
    }

    public static async Task<(Guid Id, EntityTagHeaderValue ETag)> CreateUserAsync(
        HttpClient admin, string username, Guid[]? roleIds = null, Guid? managerId = null, string password = FirstPassword)
    {
        var response = await admin.PostAsJsonAsync(
            new Uri("/api/v1/users", UriKind.Relative),
            new { username, name = $"مستخدم {username}", email = $"{username}@test.example", title = "اختبار", managerId, systemRoleIds = roleIds ?? [], password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return ((await ReadAsync(response)).GetProperty("id").GetGuid(), response.Headers.ETag!);
    }

    /// <summary>Creates a user with the given roles, signs them in with the first password and changes it (R6), so they can use the API.</summary>
    public static async Task<TestUser> ReadyUserAsync(ApiFactory factory, HttpClient admin, string prefix, params Guid[] roleIds)
    {
        var username = Unique(prefix);
        var (id, _) = await CreateUserAsync(admin, username, roleIds);

        var client = factory.CreateClient();
        var login = await LoginAsync(client, username, FirstPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        client.WithToken((await login.Content.ReadFromJsonAsync<SessionDto>())!.AccessToken);

        var change = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/password", UriKind.Relative), new { currentPassword = FirstPassword, newPassword = OwnPassword });
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
        client.WithToken((await change.Content.ReadFromJsonAsync<SessionDto>())!.AccessToken);
        return new TestUser(id, username, client);
    }

    public static async Task<(JsonElement Body, EntityTagHeaderValue ETag)> GetAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(new Uri(url, UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ReadAsync(response), response.Headers.ETag!);
    }

    public static Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string url, object? body, EntityTagHeaderValue? etag)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }

        if (etag is not null)
        {
            request.Headers.IfMatch.Add(etag);
        }

        return client.SendAsync(request);
    }

    public static async Task<(EntityTagHeaderValue ETag, JsonElement Body)> CurrentAsync(HttpClient admin, string url)
    {
        var (body, etag) = await GetAsync(admin, url);
        return (etag, body);
    }
}
