using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Ipm.IntegrationTests.Config;
using static Ipm.IntegrationTests.Identity.IdentityApi;

namespace Ipm.IntegrationTests.Projects;

/// <summary>Helpers that wrap the part 04 HTTP calls so the scenarios read like the spec.</summary>
internal static class ProjectApi
{
    public const string Start = "2026-03-01";
    public const string End = "2026-09-01";

    /// <summary>A valid project code that is unique per call (the table is shared by every test).</summary>
    public static string NewCode() => $"T{Guid.NewGuid():N}"[..8].ToUpperInvariant();

    public static async Task<Guid> UserIdAsync(HttpClient client, string username)
    {
        var directory = await ReadAsync(await client.GetAsync(new Uri("/api/v1/users/directory", UriKind.Relative)));
        return directory.EnumerateArray().Single(u => u.GetProperty("username").GetString() == username).GetProperty("id").GetGuid();
    }

    public static async Task<Guid> MyIdAsync(HttpClient client) =>
        (await ReadAsync(await client.GetAsync(new Uri("/api/v1/auth/me", UriKind.Relative)))).GetProperty("user").GetProperty("id").GetGuid();

    public static async Task<JsonElement> ProjectByCodeAsync(HttpClient client, string code)
    {
        var page = await ReadAsync(await client.GetAsync(new Uri($"/api/v1/projects?q={code}&pageSize=200", UriKind.Relative)));
        return page.GetProperty("items").EnumerateArray().Single(p => p.GetProperty("code").GetString() == code);
    }

    public static async Task<Guid> TypeIdAsync(HttpClient client, string key) =>
        (await ConfigApi.TypeByKeyAsync(client, key)).GetProperty("id").GetGuid();

    public static async Task<Guid> ProjectStatusIdAsync(HttpClient client, string key) =>
        (await WorkflowApi.StatusByKeyAsync(client, "project", key)).GetProperty("id").GetGuid();

    public static async Task<(Guid Id, EntityTagHeaderValue ETag)> CreateProjectAsync(
        HttpClient client, string? code = null, Guid? ownerId = null, Guid? templateId = null)
    {
        var owner = ownerId ?? await MyIdAsync(client);
        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/projects", UriKind.Relative),
            new { code = code ?? NewCode(), name = "مشروع اختبار", description = "وصف", ownerId = owner, startDate = Start, endDate = End, templateId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return ((await ReadAsync(response)).GetProperty("id").GetGuid(), response.Headers.ETag!);
    }

    public static object UpdateBody(string name = "اسم", object? ownerId = null, string? code = null, object? statusId = null, string start = Start, string end = End) =>
        new { name, description = "وصف", startDate = start, endDate = end, ownerId, code, statusId };

    public static object MembersBody(params (Guid User, Guid Role)[] members) =>
        new { members = members.Select(m => new { userId = m.User, roleId = m.Role }).ToArray() };

    public static async Task<(JsonElement Body, EntityTagHeaderValue ETag)> GetProjectAsync(HttpClient client, Guid id) =>
        await GetAsync(client, $"/api/v1/projects/{id}");

    public static Task<HttpResponseMessage> PutProjectAsync(HttpClient client, Guid id, object body, EntityTagHeaderValue? etag) =>
        SendAsync(client, HttpMethod.Put, $"/api/v1/projects/{id}", body, etag);

    public static Task<HttpResponseMessage> TransitionAsync(HttpClient client, Guid id, Guid toStatusId, EntityTagHeaderValue? etag, string? comment = null) =>
        SendAsync(client, HttpMethod.Post, $"/api/v1/projects/{id}/transition", new { toStatusId, comment }, etag);

    public static Task<HttpResponseMessage> PutMembersAsync(HttpClient client, Guid id, object body, EntityTagHeaderValue? etag) =>
        SendAsync(client, HttpMethod.Put, $"/api/v1/projects/{id}/members", body, etag);

    /// <summary>The project JSON of a members response (it wraps the project next to its warnings).</summary>
    public static JsonElement ProjectOf(JsonElement membersResponse) => membersResponse.GetProperty("project");
}
