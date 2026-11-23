using System.Net;
using Ipm.IntegrationTests.Git;
using Ipm.IntegrationTests.Infrastructure;
using static Ipm.IntegrationTests.Approvals.ApprovalApi;
using static Ipm.IntegrationTests.Identity.IdentityApi;
using static Ipm.IntegrationTests.Projects.ProjectApi;
using static Ipm.IntegrationTests.Work.WorkApi;

namespace Ipm.IntegrationTests.Integration;

/// <summary>docs/INTEGRATION.md: what the web app needs from the API beyond the first parts (lists that take several values, repositories with the project, delivery filters).</summary>
[Collection(ApiCollection.Name)]
public sealed class FrontEndContractTests(ApiFactory factory)
{
    [Fact]
    public async Task Work_item_filters_take_several_values_and_refuse_a_bad_id()
    {
        using var admin = await factory.AdminClientAsync();
        var (project, _) = await NewProjectAsync(admin);
        var reem = await UserIdAsync(admin, "reem");
        var fahad = await UserIdAsync(admin, "fahad");
        var (high, _, _) = await CreateAsync(admin, project, assignees: [reem]);
        var (low, _, _) = await CreateAsync(admin, project, assignees: [fahad]);
        var (none, _, _) = await CreateAsync(admin, project);
        var lowEtag = (await GetItemAsync(admin, low)).ETag;
        Assert.Equal(System.Net.HttpStatusCode.OK, (await PutItemAsync(admin, low, UpdateBody((await GetItemAsync(admin, low)).Body, priority: "low"), lowEtag)).StatusCode);

        async Task<HashSet<Guid>> Ids(string query) =>
            (await GetAsync(admin, $"/api/v1/work-items?projectId={project}&{query}")).Body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToHashSet();

        Assert.Equal([high, none], await Ids("priority=high,critical"));
        Assert.Equal([low], await Ids("priority=low"));
        Assert.Equal([high, low], await Ids($"assigneeId={reem},{fahad}"));
        Assert.Equal([high], await Ids($"assigneeId={reem}"));
        Assert.Equal(3, (await Ids(string.Empty)).Count);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(new Uri($"/api/v1/work-items?projectId={project}&statusId=not-an-id", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(new Uri($"/api/v1/work-items?projectId={project}&priority=urgent", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task A_project_carries_its_repositories_only_for_those_who_may_see_git()
    {
        using var admin = await factory.AdminClientAsync();
        using var abdullah = await factory.SignedInClientAsync("abdullah", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var (repo, _) = await GitApi.ConnectAsync(admin, project, name: "acme/with-project");

        var mine = (await GetProjectAsync(admin, project)).Body;
        Assert.Equal(repo, Assert.Single(mine.GetProperty("repositories").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.False(mine.GetProperty("repositories")[0].TryGetProperty("webhookSecret", out var secret) && secret.ValueKind == System.Text.Json.JsonValueKind.String);

        var listed = (await GetAsync(admin, $"/api/v1/projects?q=&pageSize=200")).Body.GetProperty("items").EnumerateArray().Single(p => p.GetProperty("id").GetGuid() == project);
        Assert.Equal(1, listed.GetProperty("repositories").GetArrayLength());

        // a viewer holds no git.view: the project shows no repositories
        Assert.Equal(0, (await GetProjectAsync(abdullah, project)).Body.GetProperty("repositories").GetArrayLength());
    }

    [Fact]
    public async Task Deliveries_can_be_filtered_by_outcome_and_event()
    {
        using var admin = await factory.AdminClientAsync();
        var (project, _) = await NewProjectAsync(admin);
        var (repo, _) = await GitApi.ConnectAsync(admin, project);
        await GitApi.DeliverAsync(factory, "gitea", repo, "not json");
        await GitApi.DeliverAsync(factory, "gitea", repo, GitApi.PushBody("work"));
        await GitApi.DeliverAsync(factory, "gitea", repo, GitApi.PrBody(1, "title"), eventName: "pull_request");

        async Task<int> Count(string query) => (await GetAsync(admin, $"/api/v1/git/deliveries?projectId={project}&{query}")).Body.GetProperty("total").GetInt32();

        Assert.Equal(3, await Count("pageSize=50"));
        Assert.Equal(1, await Count("outcome=failed"));
        Assert.Equal(2, await Count("outcome=processed"));
        Assert.Equal(1, await Count("event=pull_request"));
        Assert.Equal(1, await Count("outcome=processed&event=push"));
    }

    [Fact]
    public async Task An_approval_request_says_when_it_was_made()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var gated = await GatedItemAsync(admin);

        var (_, _, body) = await SubmitAsync(reem, gated.Template, "work-item", gated.Item);

        var created = body.GetProperty("createdAt").GetDateTimeOffset();
        Assert.True(Math.Abs((DateTimeOffset.UtcNow - created).TotalMinutes) < 5);
    }
}
