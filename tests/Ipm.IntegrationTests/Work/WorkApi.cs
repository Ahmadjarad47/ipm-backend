using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Ipm.IntegrationTests.Config;
using static Ipm.IntegrationTests.Identity.IdentityApi;
using static Ipm.IntegrationTests.Projects.ProjectApi;

namespace Ipm.IntegrationTests.Work;

/// <summary>Helpers that wrap the part 05 HTTP calls so the scenarios read like the spec.</summary>
internal static class WorkApi
{
    public static readonly string[] AllTypes = ["feature", "task", "bug", "subtask", "milestone"];

    public static async Task<Guid> StatusIdAsync(HttpClient client, string key) =>
        (await WorkflowApi.StatusByKeyAsync(client, "work-item", key)).GetProperty("id").GetGuid();

    /// <summary>
    /// A fresh project: the administrator owns it (project manager), reem and fahad are team members, abdullah a viewer, all five default
    /// types are allowed. Returns its id and code.
    /// </summary>
    public static async Task<(Guid Id, string Code)> NewProjectAsync(HttpClient admin, params string[] typeKeys)
    {
        var code = NewCode();
        var owner = await MyIdAsync(admin);
        var (id, etag) = await CreateProjectAsync(admin, code);

        var manager = await RoleIdAsync(admin, "project_manager");
        var member = await RoleIdAsync(admin, "team_member");
        var viewer = await RoleIdAsync(admin, "viewer");
        var members = await PutMembersAsync(
            admin, id,
            MembersBody(
                (owner, manager), (await UserIdAsync(admin, "reem"), member), (await UserIdAsync(admin, "fahad"), member),
                (await UserIdAsync(admin, "abdullah"), viewer)),
            etag);
        Assert.Equal(HttpStatusCode.OK, members.StatusCode);

        var typeIds = new List<Guid>();
        foreach (var key in typeKeys.Length == 0 ? AllTypes : typeKeys)
        {
            typeIds.Add(await TypeIdAsync(admin, key));
        }

        var allowed = await SendAsync(
            admin, HttpMethod.Put, $"/api/v1/projects/{id}/work-item-types", new { allowedTypeIds = typeIds, defaultTypeId = typeIds[0] }, members.Headers.ETag);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        return (id, code);
    }

    /// <summary>Creates an item and asserts it worked. Features and bugs get the field their type requires unless <paramref name="fields"/> is given.</summary>
    public static async Task<(Guid Id, EntityTagHeaderValue ETag, JsonElement Body)> CreateAsync(
        HttpClient client, Guid projectId, string typeKey = "task", string title = "عنصر اختبار", Guid? parentId = null, Guid[]? assignees = null,
        object? fields = null, string? dueDate = null)
    {
        var response = await PostItemAsync(client, projectId, typeKey, title, parentId, assignees, fields, dueDate);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadAsync(response);
        return (body.GetProperty("id").GetGuid(), response.Headers.ETag!, body);
    }

    public static async Task<HttpResponseMessage> PostItemAsync(
        HttpClient client, Guid projectId, string typeKey = "task", string title = "عنصر اختبار", Guid? parentId = null, Guid[]? assignees = null,
        object? fields = null, string? dueDate = null)
    {
        object? values = fields ?? typeKey switch
        {
            "feature" => new { business_value = "high" },
            "bug" => new { severity = "minor" },
            _ => null,
        };

        return await client.PostAsJsonAsync(
            new Uri("/api/v1/work-items", UriKind.Relative),
            new { projectId, typeId = await TypeIdAsync(client, typeKey), title, priority = "high", dueDate, parentId, assigneeIds = assignees ?? [], fields = values });
    }

    public static Task<(JsonElement Body, EntityTagHeaderValue ETag)> GetItemAsync(HttpClient client, Guid id) => GetAsync(client, $"/api/v1/work-items/{id}");

    public static Task<HttpResponseMessage> TransitionItemAsync(HttpClient client, Guid id, Guid toStatusId, EntityTagHeaderValue? etag, string? comment = null) =>
        SendAsync(client, HttpMethod.Post, $"/api/v1/work-items/{id}/transition", new { toStatusId, comment }, etag);

    public static Task<HttpResponseMessage> PutItemAsync(HttpClient client, Guid id, object body, EntityTagHeaderValue? etag) =>
        SendAsync(client, HttpMethod.Put, $"/api/v1/work-items/{id}", body, etag);

    /// <summary>The update body that repeats the item and changes only what <paramref name="title"/>, <paramref name="assignees"/> and <paramref name="priority"/> say.</summary>
    public static object UpdateBody(JsonElement item, string? title = null, Guid[]? assignees = null, string? priority = null, object? fields = null) => new
    {
        title = title ?? item.GetProperty("title").GetString(),
        description = item.GetProperty("description").GetString(),
        priority = priority ?? item.GetProperty("priority").GetString(),
        startDate = item.GetProperty("startDate").ValueKind == JsonValueKind.Null ? null : item.GetProperty("startDate").GetString(),
        dueDate = item.GetProperty("dueDate").ValueKind == JsonValueKind.Null ? null : item.GetProperty("dueDate").GetString(),
        assigneeIds = assignees ?? item.GetProperty("assigneeIds").EnumerateArray().Select(a => a.GetGuid()).ToArray(),
        fields = fields ?? item.GetProperty("fields"),
    };

    /// <summary>Moves an item along the standard workflow with a fresh ETag each time. Returns the last response.</summary>
    public static async Task<HttpResponseMessage> MoveAsync(HttpClient client, Guid id, params string[] statusKeys)
    {
        HttpResponseMessage? last = null;
        foreach (var key in statusKeys)
        {
            var (_, etag) = await GetItemAsync(client, id);
            last = await TransitionItemAsync(client, id, await StatusIdAsync(client, key), etag);
            Assert.Equal(HttpStatusCode.OK, last.StatusCode);
        }

        return last!;
    }

    public static async Task<JsonElement> BulkAsync(HttpClient client, Guid[] ids, object action, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var response = await client.PostAsJsonAsync(new Uri("/api/v1/work-items/bulk", UriKind.Relative), new { ids, action });
        Assert.Equal(expected, response.StatusCode);
        return await ReadAsync(response);
    }
}
