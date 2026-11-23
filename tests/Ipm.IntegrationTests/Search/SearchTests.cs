using System.Text.Json;
using Ipm.IntegrationTests.Infrastructure;
using static Ipm.IntegrationTests.Approvals.ApprovalApi;
using static Ipm.IntegrationTests.Identity.IdentityApi;
using static Ipm.IntegrationTests.Projects.ProjectApi;
using static Ipm.IntegrationTests.Work.WorkApi;

namespace Ipm.IntegrationTests.Search;

/// <summary>Scenarios 13 and 14 of docs/modules/09-dashboard-search-settings.md: the global search and what it may show.</summary>
[Collection(ApiCollection.Name)]
public sealed class SearchTests(ApiFactory factory)
{
    private static async Task<List<JsonElement>> SearchAsync(HttpClient client, string q, int? limit = null) =>
        [.. (await GetAsync(client, $"/api/v1/search?q={Uri.EscapeDataString(q)}" + (limit is null ? string.Empty : $"&limit={limit}"))).Body.EnumerateArray()];

    private static string Token() => $"zq{Guid.NewGuid():N}"[..12];

    [Fact]
    public async Task Scenario_13_R12_to_R15_the_text_rules_the_order_and_the_hit_shape()
    {
        using var admin = await factory.AdminClientAsync();
        var (project, _) = await NewProjectAsync(admin);
        var token = Token();
        var (itemId, _, item) = await CreateAsync(admin, project, title: $"{token}Alpha");
        var reference = item.GetProperty("ref").GetString()!;

        Assert.Empty(await SearchAsync(admin, "z"));
        Assert.Empty(await SearchAsync(admin, "  "));

        // case does not matter, the hit says where to go, and the exact code comes first
        var byTitle = await SearchAsync(admin, token.ToUpperInvariant());
        var hit = Assert.Single(byTitle);
        Assert.Equal("work-item", hit.GetProperty("kind").GetString());
        Assert.Equal(itemId, hit.GetProperty("id").GetGuid());
        Assert.Equal($"/work-items/{itemId}", hit.GetProperty("link").GetString());
        Assert.Equal(reference, hit.GetProperty("code").GetString());
        Assert.Equal(await TypeIdAsync(admin, "task"), hit.GetProperty("typeId").GetGuid());

        var byCode = await SearchAsync(admin, reference.ToLowerInvariant());
        Assert.Equal(itemId, byCode[0].GetProperty("id").GetGuid());

        // percent and underscore are letters, not patterns
        Assert.Empty(await SearchAsync(admin, $"{token[..4]}%{token[6..]}"));
        Assert.Empty(await SearchAsync(admin, $"{token[..4]}_{token[5..]}"));

        Assert.True((await SearchAsync(admin, "ERP", limit: 100)).Count <= 30);
        Assert.True((await SearchAsync(admin, "ERP", limit: 2)).Count <= 2);
        Assert.Contains(await SearchAsync(admin, "ERP"), h => h.GetProperty("kind").GetString() == "project");
    }

    [Fact]
    public async Task Scenario_14_R13_nobody_finds_what_they_may_not_see()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        using var noura = await factory.SignedInClientAsync("noura", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var token = Token();
        await CreateAsync(admin, project, title: $"{token}Beta");

        Assert.Single(await SearchAsync(reem, token));
        Assert.Empty(await SearchAsync(noura, token));

        // users only for those who manage roles
        Assert.Contains(await SearchAsync(admin, "reem"), h => h.GetProperty("kind").GetString() == "user");
        Assert.DoesNotContain(await SearchAsync(reem, "reem"), h => h.GetProperty("kind").GetString() == "user");

        // an approval request shows to its requester and to nobody outside the project
        var gated = await GatedItemAsync(admin);
        var (_, _, body) = await SubmitAsync(reem, gated.Template, "work-item", gated.Item);
        var title = body.GetProperty("title").GetString()!;
        Assert.Contains(await SearchAsync(admin, title), h => h.GetProperty("kind").GetString() == "approval");
        Assert.DoesNotContain(await SearchAsync(noura, title), h => h.GetProperty("kind").GetString() == "approval");

        // an archived item is gone from the results
var (gone, tag, _) = await CreateAsync(admin, project, title: $"{token}Gone");        Assert.Single(await SearchAsync(admin, $"{token}Gone"));        Assert.Equal(System.Net.HttpStatusCode.NoContent, (await SendAsync(admin, HttpMethod.Delete, $"/api/v1/work-items/{gone}", null, tag)).StatusCode);        Assert.Empty(await SearchAsync(admin, $"{token}Gone"));
    }
}
