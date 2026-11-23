using System.Net;
using System.Net.Http.Json;
using Ipm.Application.Abstractions.Git;
using Ipm.Domain.Common;
using Ipm.IntegrationTests.Infrastructure;
using static Ipm.IntegrationTests.Git.GitApi;
using static Ipm.IntegrationTests.Identity.IdentityApi;
using static Ipm.IntegrationTests.Projects.ProjectApi;
using static Ipm.IntegrationTests.Work.WorkApi;

namespace Ipm.IntegrationTests.Git;

/// <summary>Scenarios 4 (ref pattern), 10 (sync), 11 (events list), 12 (dev indicator) and the demo data of docs/modules/07-git-integration.md.</summary>
[Collection(ApiCollection.Name)]
public sealed class GitSyncAndIndicatorTests(ApiFactory factory)
{
    private static Task<HttpResponseMessage> SetPatternAsync(HttpClient client, Guid project, string? pattern, System.Net.Http.Headers.EntityTagHeaderValue? etag) =>
        SendAsync(client, HttpMethod.Put, $"/api/v1/projects/{project}/ref-pattern", new { refPattern = pattern }, etag);

    [Fact]
    public async Task Scenario_10_R10_sync_reads_the_latest_commits_links_them_and_a_repeat_adds_nothing()
    {
        using var admin = await factory.AdminClientAsync();
        var (project, _) = await NewProjectAsync(admin);
        var (repo, _) = await ConnectAsync(admin, project);
        var (_, _, item) = await CreateAsync(admin, project);
        var reference = item.GetProperty("ref").GetString()!;

        factory.GitRemote.Next = new List<RemoteCommit>
        {
            new("1111111aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa1", $"{reference} first", "dev", "https://git.example.com/c/1"),
            new("2222222bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb2", "no ref here", "dev", "https://git.example.com/c/2"),
        };
        var uri = new Uri($"/api/v1/projects/{project}/git-sync", UriKind.Relative);

        var first = await admin.PostAsJsonAsync(uri, new { repositoryId = repo });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var body = await ReadAsync(first);
        Assert.Equal(2, body.GetProperty("fetched").GetInt32());
        Assert.Equal(2, body.GetProperty("ingested").GetInt32());
        Assert.Contains(reference, body.GetProperty("linkedRefs").ToString(), StringComparison.Ordinal);
        Assert.Equal("tok-abc-123", factory.GitRemote.LastToken);
        Assert.NotEqual(System.Text.Json.JsonValueKind.Null, body.GetProperty("repository").GetProperty("lastSyncAt").ValueKind);

        var second = await ReadAsync(await admin.PostAsJsonAsync(uri, new { repositoryId = repo }));
        Assert.Equal(0, second.GetProperty("ingested").GetInt32());
        Assert.Equal(2, (await EventsAsync(admin, project)).GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Scenario_10_R10_a_failing_git_server_is_a_502_and_the_error_is_kept_on_the_repository()
    {
        using var admin = await factory.AdminClientAsync();
        var (project, _) = await NewProjectAsync(admin);
        var (repo, _) = await ConnectAsync(admin, project);

        factory.GitRemote.Next = Error.BadGateway("Git.RemoteFailed", "تعذّر الاتصال بخادم Git.");
        var response = await admin.PostAsJsonAsync(new Uri($"/api/v1/projects/{project}/git-sync", UriKind.Relative), new { repositoryId = repo });
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);

        var list = await ReadAsync(await admin.GetAsync(new Uri($"/api/v1/projects/{project}/repositories", UriKind.Relative)));
        Assert.False(string.IsNullOrEmpty(list[0].GetProperty("lastSyncError").GetString()));
        factory.GitRemote.Next = new List<RemoteCommit>();
    }

    [Fact]
    public async Task Scenario_4_R4_a_custom_ref_pattern_is_checked_against_real_items_and_replaces_the_default()
    {
        using var admin = await factory.AdminClientAsync();
        var (project, code) = await NewProjectAsync(admin);
        var (repo, _) = await ConnectAsync(admin, project);
        var (_, _, item) = await CreateAsync(admin, project);
        var reference = item.GetProperty("ref").GetString()!;


        // a pattern that finds nothing in the items, one with two groups, one with a look-behind and one that is too long
        var (_, etag) = await GetProjectAsync(admin, project);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await SetPatternAsync(admin, project, @"\bNOPE-(\d+)\b", etag)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await SetPatternAsync(admin, project, @"(\w+)-(\d+)", etag)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await SetPatternAsync(admin, project, @"(?<=x)(\w+-\d+)", etag)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await SetPatternAsync(admin, project, new string('a', 101), etag)).StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionRequired, (await SetPatternAsync(admin, project, null, null)).StatusCode);

        var accepted = await SetPatternAsync(admin, project, $@"\[({code}-\d+)\]", etag);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(System.Text.Json.JsonValueKind.String, (await ReadAsync(accepted)).GetProperty("refPattern").ValueKind);

        Assert.Equal(0, (await ReadAsync(await DeliverAsync(factory, "gitea", repo, PushBody($"{reference} plain")))).GetProperty("linkedRefs").GetArrayLength());
        Assert.Equal(1, (await ReadAsync(await DeliverAsync(factory, "gitea", repo, PushBody($"[{reference}] bracketed")))).GetProperty("linkedRefs").GetArrayLength());

        var (_, etag2) = await GetProjectAsync(admin, project);
        var reset = await SetPatternAsync(admin, project, null, etag2);
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, (await ReadAsync(reset)).GetProperty("refPattern").ValueKind);
    }

    [Fact]
    public async Task Scenario_12_R13_the_dev_indicator_counts_commits_and_pull_requests_and_is_hidden_without_git_view()
    {
        using var admin = await factory.AdminClientAsync();
        using var abdullah = await factory.SignedInClientAsync("abdullah", ApiFactory.DemoPassword);
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var (repo, _) = await ConnectAsync(admin, project);
        var (id, _, item) = await CreateAsync(admin, project);
        var reference = item.GetProperty("ref").GetString()!;

        await DeliverAsync(factory, "gitea", repo, PushBody($"{reference} one"));
        await DeliverAsync(factory, "gitea", repo, PushBody($"{reference} two"));
        await DeliverAsync(factory, "gitea", repo, PrBody(1, $"{reference} open pr"), eventName: "pull_request");
        await DeliverAsync(factory, "gitea", repo, PrBody(2, $"{reference} merged pr", action: "closed", merged: true), eventName: "pull_request");

        var dev = (await GetItemAsync(admin, id)).Body.GetProperty("dev");
        Assert.Equal(2, dev.GetProperty("commits").GetInt32());
        Assert.Equal(1, dev.GetProperty("openPrs").GetInt32());
        Assert.Equal(1, dev.GetProperty("mergedPrs").GetInt32());

        var listed = (await GetAsync(admin, $"/api/v1/work-items?projectId={project}")).Body.GetProperty("items")[0];
        Assert.Equal(2, listed.GetProperty("dev").GetProperty("commits").GetInt32());

        Assert.True((await GetItemAsync(reem, id)).Body.TryGetProperty("dev", out var seen) && seen.ValueKind == System.Text.Json.JsonValueKind.Object);
        var hidden = (await GetItemAsync(abdullah, id)).Body;
        Assert.True(!hidden.TryGetProperty("dev", out var none) || none.ValueKind == System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task Scenario_11_R12_events_can_be_filtered_and_need_git_view()
    {
        using var admin = await factory.AdminClientAsync();
        using var abdullah = await factory.SignedInClientAsync("abdullah", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var (repo, _) = await ConnectAsync(admin, project);
        var (_, _, item) = await CreateAsync(admin, project);
        var reference = item.GetProperty("ref").GetString()!;

        await DeliverAsync(factory, "gitea", repo, PushBody($"{reference} login page"));
        await DeliverAsync(factory, "gitea", repo, PrBody(3, $"{reference} billing"), eventName: "pull_request");

        Assert.Equal(2, (await EventsAsync(admin, project, reference)).GetProperty("items").GetArrayLength());
        Assert.Equal(1, (await EventsAsync(admin, project, kind: "push")).GetProperty("items").GetArrayLength());
        var search = (await GetAsync(admin, $"/api/v1/git-events?projectId={project}&search=billing")).Body.GetProperty("items");
        Assert.Equal(1, search.GetArrayLength());

        var forbidden = await abdullah.GetAsync(new Uri($"/api/v1/git-events?projectId={project}", UriKind.Relative));
        Assert.True(forbidden.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound);
        Assert.Equal(HttpStatusCode.Forbidden, (await abdullah.GetAsync(new Uri($"/api/v1/git/deliveries?projectId={project}", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(new Uri("/api/v1/git/deliveries", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task The_demo_data_has_three_repositories_and_four_events_without_any_secret()
    {
        using var admin = await factory.AdminClientAsync();
        var erp = await ProjectByCodeAsync(admin, "ERP");
        var repos = await ReadAsync(await admin.GetAsync(new Uri($"/api/v1/projects/{erp.GetProperty("id").GetGuid()}/repositories", UriKind.Relative)));
        Assert.True(repos.GetArrayLength() >= 2);
        Assert.All(repos.EnumerateArray(), r => Assert.True(r.GetProperty("hasCredential").GetBoolean()));
        Assert.DoesNotContain("webhookSecret\":\"", repos.GetRawText(), StringComparison.Ordinal);

        var events = (await GetAsync(admin, $"/api/v1/git-events?projectId={erp.GetProperty("id").GetGuid()}&workItemRef=ERP-104")).Body.GetProperty("items");
        Assert.True(events.GetArrayLength() >= 1);
        var merged = (await GetAsync(admin, $"/api/v1/git-events?projectId={erp.GetProperty("id").GetGuid()}&workItemRef=ERP-111&kind=merge")).Body.GetProperty("items");
        Assert.True(merged.GetArrayLength() >= 1);
    }
}
