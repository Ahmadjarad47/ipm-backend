using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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
