using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using static Ipm.IntegrationTests.Config.ConfigApi;
using static Ipm.IntegrationTests.Identity.IdentityApi;

namespace Ipm.IntegrationTests.Config;

/// <summary>Helpers that wrap the part 02 HTTP calls so the scenarios read like the spec.</summary>
internal static class ConfigApi
{
    /// <summary>A key that is valid and unique per call (the table is shared by every test).</summary>
    public static string NewKey(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 9, 31)];

    public static async Task<JsonElement> TypeByKeyAsync(HttpClient client, string key)
    {
        var list = await ReadAsync(await client.GetAsync(new Uri("/api/v1/work-item-types", UriKind.Relative)));
        return list.EnumerateArray().Single(t => t.GetProperty("key").GetString() == key);
    }

    public static async Task<JsonElement> RelationTypeByKeyAsync(HttpClient client, string key)
    {
        var list = await ReadAsync(await client.GetAsync(new Uri("/api/v1/relation-types", UriKind.Relative)));
        return list.EnumerateArray().Single(t => t.GetProperty("key").GetString() == key);
    }

    public static async Task<(Guid Id, EntityTagHeaderValue ETag)> CreateTypeAsync(HttpClient admin, string key, string name = "نوع اختبار")
    {
        var response = await admin.PostAsJsonAsync(
            new Uri("/api/v1/work-item-types", UriKind.Relative),
            new { key, name, description = "وصف", color = "#112233", icon = "task", order = 9, active = true });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return ((await ReadAsync(response)).GetProperty("id").GetGuid(), response.Headers.ETag!);
    }

    public static async Task<(Guid Id, EntityTagHeaderValue ETag)> CreateRelationTypeAsync(HttpClient admin, string key, bool directed = true, bool acyclic = false)
    {
        var response = await admin.PostAsJsonAsync(
            new Uri("/api/v1/relation-types", UriKind.Relative),
            new { key, name = "يرتبط", inverseName = directed ? "مرتبط عكسياً" : "يرتبط", directed, acyclic, active = true });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return ((await ReadAsync(response)).GetProperty("id").GetGuid(), response.Headers.ETag!);
    }

    public static object TypeBody(string name = "اسم", string color = "#112233", string icon = "task", object? order = null, object? active = null, string? key = null) =>
        new { name, description = "وصف", color, icon, order, active, key };

    public static object Field(string key, string type = "text", object? options = null, double? min = null, double? max = null, int order = 0) =>
        new { key, label = "حقل", type, required = false, options, min, max, showInList = false, showOnCard = false, order };
}

/// <summary>Helpers for statuses and workflows (part 03).</summary>
internal static class WorkflowApi
{
    public static async Task<JsonElement> StatusByKeyAsync(HttpClient client, string scope, string key)
    {
        var list = await ReadAsync(await client.GetAsync(new Uri($"/api/v1/statuses?scope={scope}", UriKind.Relative)));
        return list.EnumerateArray().Single(s => s.GetProperty("key").GetString() == key);
    }

    public static async Task<JsonElement> WorkflowByKeyAsync(HttpClient client, string key)
    {
        var list = await ReadAsync(await client.GetAsync(new Uri("/api/v1/workflows", UriKind.Relative)));
        return list.EnumerateArray().Single(w => w.GetProperty("key").GetString() == key);
    }

    public static async Task<(Guid Id, EntityTagHeaderValue ETag)> CreateStatusAsync(
        HttpClient admin, string key, string scope = "work-item", string category = "active", string name = "حالة اختبار")
    {
        var response = await admin.PostAsJsonAsync(
            new Uri("/api/v1/statuses", UriKind.Relative),
            new { key, name, description = "وصف", color = "#112233", category, scope, order = 9, active = true });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return ((await ReadAsync(response)).GetProperty("id").GetGuid(), response.Headers.ETag!);
    }

    public static object Move(Guid from, Guid to, string? permission = null, bool assignee = false, bool approval = false, bool dependencies = false) =>
        new { fromStatusId = from, toStatusId = to, permission, requiresAssignee = assignee, requiresApproval = approval, blockedByOpenDependencies = dependencies };

    public static object WorkflowBody(string key, Guid[] statusIds, Guid initial, object[] transitions, string scope = "work-item", string name = "سير اختبار") =>
        new { key, name, scope, statusIds, initialStatusId = initial, transitions };

    /// <summary>Creates a work item workflow of two fresh statuses (a to b) and returns everything a test needs to edit it.</summary>
    public static async Task<(Guid Id, EntityTagHeaderValue ETag, Guid A, Guid B)> CreateWorkflowAsync(HttpClient admin, string key)
    {
        var (a, _) = await CreateStatusAsync(admin, NewKey("sa"));
        var (b, _) = await CreateStatusAsync(admin, NewKey("sb"));
        var response = await admin.PostAsJsonAsync(
            new Uri("/api/v1/workflows", UriKind.Relative), WorkflowBody(key, [a, b], a, [Move(a, b)]));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return ((await ReadAsync(response)).GetProperty("id").GetGuid(), response.Headers.ETag!, a, b);
    }
}
