using System.Net;
using System.Net.Http.Json;
using Ipm.IntegrationTests.Infrastructure;
using static Ipm.IntegrationTests.Git.GitApi;
using static Ipm.IntegrationTests.Identity.IdentityApi;
using static Ipm.IntegrationTests.Projects.ProjectApi;
using static Ipm.IntegrationTests.Work.WorkApi;

namespace Ipm.IntegrationTests.Git;

/// <summary>Scenarios 1, 3, 4 and 14 of docs/modules/07-git-integration.md: connecting, rotating, disconnecting, archived projects.</summary>
[Collection(ApiCollection.Name)]
public sealed class GitConnectionTests(ApiFactory factory)
{
    private static object Body(string url = "https://git.example.com/acme/app", string name = "acme/app", string token = "tok", string? secret = null) =>
        new { provider = "gitea", name, url, defaultBranch = "main", accessToken = token, webhookSecret = secret };

    [Fact]
    public async Task Scenario_1_R1_connect_validates_the_url_name_and_secret_and_never_returns_the_token()
    {
        using var admin = await factory.AdminClientAsync();
        var (project, _) = await NewProjectAsync(admin);
        var uri = new Uri($"/api/v1/projects/{project}/repositories", UriKind.Relative);

        foreach (var bad in new[] { "http://git.example.com/a/b", "https://localhost/a/b", "https://10.0.0.5/a/b", "https://user:pw@git.example.com/a/b", "https://git.example.com:8443/a/b", "https://[::ffff:10.0.0.1]/a/b" })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(uri, Body(url: bad))).StatusCode);
        }

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(uri, Body(name: "no-slash"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(uri, Body(secret: "short"))).StatusCode);

        var ok = await admin.PostAsJsonAsync(uri, Body());
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        var created = await ReadAsync(ok);
        Assert.False(string.IsNullOrEmpty(created.GetProperty("webhookSecret").GetString()));
        Assert.True(created.GetProperty("hasCredential").GetBoolean());
        Assert.StartsWith("/api/v1/webhooks/gitea/", created.GetProperty("webhookUrl").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain("\"tok\"", created.ToString(), StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(uri, Body())).StatusCode);

        var list = await ReadAsync(await admin.GetAsync(uri));
        Assert.Equal(1, list.GetArrayLength());
        Assert.True(!list[0].TryGetProperty("webhookSecret", out var again) || again.ValueKind == System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task Scenario_1_connecting_needs_git_manage_and_a_viewer_cannot()
    {
        using var admin = await factory.AdminClientAsync();
        using var abdullah = await factory.SignedInClientAsync("abdullah", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var uri = new Uri($"/api/v1/projects/{project}/repositories", UriKind.Relative);

        Assert.Equal(HttpStatusCode.Forbidden, (await abdullah.PostAsJsonAsync(uri, Body())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await abdullah.GetAsync(uri)).StatusCode);
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        Assert.Equal(HttpStatusCode.OK, (await reem.GetAsync(uri)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reem.PostAsJsonAsync(uri, Body())).StatusCode);
    }

    [Fact]
    public async Task Scenario_3_R3_rotating_the_secret_returns_it_once_and_the_old_one_stops_working()
    {
        using var admin = await factory.AdminClientAsync();
        var (project, _) = await NewProjectAsync(admin);
        var (repo, _) = await ConnectAsync(admin, project);

        var rotated = await admin.PostAsync(new Uri($"/api/v1/projects/{project}/repositories/{repo}/rotate-secret", UriKind.Relative), null);
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        var fresh = (await ReadAsync(rotated)).GetProperty("secret").GetString()!;
        Assert.NotEqual(Secret, fresh);

        Assert.Equal(HttpStatusCode.Unauthorized, (await DeliverAsync(factory, "gitea", repo, PushBody("x"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await DeliverAsync(factory, "gitea", repo, PushBody("x"), secret: fresh)).StatusCode);
    }

    [Fact]
    public async Task Scenario_4_R2_disconnect_wipes_credentials_and_later_deliveries_are_refused()
    {
        using var admin = await factory.AdminClientAsync();
        var (project, _) = await NewProjectAsync(admin);
        var (repo, _) = await ConnectAsync(admin, project, name: "acme/gone");

        var response = await admin.PostAsync(new Uri($"/api/v1/projects/{project}/repositories/{repo}/disconnect", UriKind.Relative), null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.False(body.GetProperty("connected").GetBoolean());
        Assert.False(body.GetProperty("hasCredential").GetBoolean());

        Assert.Equal(HttpStatusCode.Unauthorized, (await DeliverAsync(factory, "gitea", repo, PushBody("x"))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PostAsync(new Uri($"/api/v1/projects/{project}/repositories/{repo}/rotate-secret", UriKind.Relative), null)).StatusCode);

        await ConnectAsync(admin, project, name: "acme/gone");
    }

    [Fact]
    public async Task Scenario_4_R2_disconnect_clears_the_sync_error_and_reconnecting_reuses_the_record_and_its_events()
    {
        using var admin = await factory.AdminClientAsync();
        var (project, _) = await NewProjectAsync(admin);
        var (repo, _) = await ConnectAsync(admin, project, name: "acme/again");
        var reposUri = new Uri($"/api/v1/projects/{project}/repositories", UriKind.Relative);
        Assert.Equal(HttpStatusCode.OK, (await DeliverAsync(factory, "gitea", repo, PushBody("before"))).StatusCode);

        factory.GitRemote.Next = Ipm.Domain.Common.Error.BadGateway("Git.RemoteFailed", "رفض خادم Git الطلب (401).");
        Assert.Equal(HttpStatusCode.BadGateway, (await admin.PostAsJsonAsync(new Uri($"/api/v1/projects/{project}/git-sync", UriKind.Relative), new { repositoryId = repo })).StatusCode);
        factory.GitRemote.Next = new List<Ipm.Application.Abstractions.Git.RemoteCommit>();

        var disconnected = await ReadAsync(await admin.PostAsync(new Uri($"/api/v1/projects/{project}/repositories/{repo}/disconnect", UriKind.Relative), null));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, disconnected.GetProperty("lastSyncError").ValueKind);

        var again = await admin.PostAsJsonAsync(reposUri, Body(url: "https://git.example.com/acme/again", name: "acme/again", token: "tok-new"));
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
        var reconnected = await ReadAsync(again);
        Assert.Equal(repo, reconnected.GetProperty("id").GetGuid());
        Assert.True(reconnected.GetProperty("connected").GetBoolean());
        Assert.True(reconnected.GetProperty("hasCredential").GetBoolean());
        Assert.False(string.IsNullOrEmpty(reconnected.GetProperty("webhookSecret").GetString()));

        Assert.Equal(1, (await ReadAsync(await admin.GetAsync(reposUri))).GetArrayLength());
        Assert.Equal(1, (await EventsAsync(admin, project)).GetProperty("items").GetArrayLength());

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync(new Uri($"/api/v1/projects/{project}/git-sync", UriKind.Relative), new { repositoryId = repo })).StatusCode);
        Assert.Equal("tok-new", factory.GitRemote.LastToken);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(reposUri, Body(name: "acme/again"))).StatusCode);
    }

    [Fact]
    public async Task Scenario_14_R14_an_archived_project_refuses_connecting_but_still_ingests()
    {
        using var admin = await factory.AdminClientAsync();
        var (project, _) = await NewProjectAsync(admin);
        var (repo, _) = await ConnectAsync(admin, project);
        var (_, _, itemBody) = await CreateAsync(admin, project);
        var reference = itemBody.GetProperty("ref").GetString()!;

        var (_, etag) = await GetProjectAsync(admin, project);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(admin, HttpMethod.Post, $"/api/v1/projects/{project}/archive", null, etag)).StatusCode);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PostAsJsonAsync(new Uri($"/api/v1/projects/{project}/repositories", UriKind.Relative), Body(name: "acme/late"))).StatusCode);
        var delivered = await DeliverAsync(factory, "gitea", repo, PushBody($"{reference} still works"));
        Assert.Equal(HttpStatusCode.OK, delivered.StatusCode);
        Assert.Contains(reference, (await ReadAsync(delivered)).GetProperty("linkedRefs").ToString(), StringComparison.Ordinal);
    }
}
