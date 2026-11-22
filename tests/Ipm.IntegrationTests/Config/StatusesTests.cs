using System.Net;
using System.Net.Http.Json;
using Ipm.Domain.Identity;
using Ipm.IntegrationTests.Infrastructure;
using static Ipm.IntegrationTests.Config.ConfigApi;
using static Ipm.IntegrationTests.Config.WorkflowApi;
using static Ipm.IntegrationTests.Identity.IdentityApi;

namespace Ipm.IntegrationTests.Config;

/// <summary>Scenarios 1–5, 6, 10 and 11 of docs/modules/03-workflows.md for statuses.</summary>
[Collection(ApiCollection.Name)]
public sealed class StatusesTests(ApiFactory factory)
{
    private static readonly Uri Statuses = new("/api/v1/statuses", UriKind.Relative);

    [Fact]
    public async Task Scenario_1_any_signed_in_user_reads_the_twelve_default_statuses()
    {
        using var noura = await factory.SignedInClientAsync("noura", ApiFactory.DemoPassword);

        var all = (await ReadAsync(await noura.GetAsync(Statuses))).EnumerateArray().ToList();
        Assert.True(all.Count >= 12);

        var projects = await ReadAsync(await noura.GetAsync(new Uri("/api/v1/statuses?scope=project", UriKind.Relative)));
        Assert.All(projects.EnumerateArray(), s => Assert.Equal("project", s.GetProperty("scope").GetString()));
        // Other tests add statuses of their own, so only the order of the defaults is checked.
        string[] projectDefaults = ["pending", "active", "on_hold", "completed", "cancelled"];
        Assert.Equal(
            projectDefaults,
            projects.EnumerateArray().Select(s => s.GetProperty("key").GetString()!).Where(projectDefaults.Contains));

        var items = (await ReadAsync(await noura.GetAsync(new Uri("/api/v1/statuses?scope=work-item", UriKind.Relative)))).EnumerateArray().ToList();
        string[] itemDefaults = ["pending", "in_progress", "in_review", "blocked", "completed", "rejected", "cancelled"];
        Assert.Equal(
            itemDefaults,
            items.Select(s => s.GetProperty("key").GetString()!).Where(itemDefaults.Contains));

        var pending = await StatusByKeyAsync(noura, "project", "pending");
        Assert.Equal("not-started", pending.GetProperty("category").GetString());
        Assert.Equal("done", (await StatusByKeyAsync(noura, "work-item", "completed")).GetProperty("category").GetString());

        var one = await noura.GetAsync(new Uri($"/api/v1/statuses/{pending.GetProperty("id").GetGuid()}", UriKind.Relative));
        Assert.NotNull(one.Headers.ETag);
        Assert.Equal(HttpStatusCode.BadRequest, (await noura.GetAsync(new Uri("/api/v1/statuses?scope=galaxy", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task Scenario_1_without_a_token_nothing_is_readable()
    {
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Statuses)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(new Uri("/api/v1/workflows", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task Scenario_2_an_ordinary_user_cannot_create_a_status_but_the_administrator_can()
    {
        using var noura = await factory.SignedInClientAsync("noura", ApiFactory.DemoPassword);
        using var admin = await factory.AdminClientAsync();
        var body = new { key = NewKey("st"), name = "حالة", color = "#112233", category = "active", scope = "work-item" };

        Assert.Equal(HttpStatusCode.Forbidden, (await noura.PostAsJsonAsync(Statuses, body)).StatusCode);
        var created = await admin.PostAsJsonAsync(Statuses, body);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.ETag);
        var status = await ReadAsync(created);
        Assert.True(status.GetProperty("active").GetBoolean());
        Assert.Equal("active", status.GetProperty("category").GetString());
    }

    [Fact]
    public async Task Scenario_3_statuses_and_workflows_are_separate_permissions()
    {
        using var admin = await factory.AdminClientAsync();
        var (statusRole, _) = await CreateRoleAsync(admin, Unique("stat"), RoleScope.System, [Permissions.StatusesManage]);
        var (workflowRole, _) = await CreateRoleAsync(admin, Unique("flow"), RoleScope.System, [Permissions.WorkflowsManage]);
        var statusManager = await ReadyUserAsync(factory, admin, "sm", statusRole);
        var workflowManager = await ReadyUserAsync(factory, admin, "wm", workflowRole);

        var (a, _) = await CreateStatusAsync(admin, NewKey("sa"));
        var (b, _) = await CreateStatusAsync(admin, NewKey("sb"));
        var statusBody = new { key = NewKey("st"), name = "حالة", color = "#112233", category = "active", scope = "work-item" };
        var workflowBody = WorkflowBody(NewKey("wf"), [a, b], a, [Move(a, b)]);

        Assert.Equal(HttpStatusCode.Created, (await statusManager.Client.PostAsJsonAsync(Statuses, statusBody)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await statusManager.Client.PostAsJsonAsync(new Uri("/api/v1/workflows", UriKind.Relative), workflowBody)).StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await workflowManager.Client.PostAsJsonAsync(new Uri("/api/v1/workflows", UriKind.Relative), workflowBody)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await workflowManager.Client.PostAsJsonAsync(Statuses, statusBody)).StatusCode);
    }

    [Fact]
    public async Task Scenario_4_R1_a_key_is_unique_inside_its_scope_well_formed_and_never_changes()
    {
        using var admin = await factory.AdminClientAsync();
        var key = NewKey("ky");
        var (id, etag) = await CreateStatusAsync(admin, key, "work-item");

        var duplicate = await admin.PostAsJsonAsync(
            Statuses, new { key = key.ToUpperInvariant(), name = "x", color = "#112233", category = "active", scope = "work-item" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("Status.KeyTaken", (await ProblemAsync(duplicate)).Code);

        var otherScope = await admin.PostAsJsonAsync(Statuses, new { key, name = "x", color = "#112233", category = "active", scope = "project" });
        Assert.Equal(HttpStatusCode.Created, otherScope.StatusCode);

        var malformed = await admin.PostAsJsonAsync(Statuses, new { key = "1 bad", name = "x", color = "#112233", category = "active", scope = "project" });
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Equal("Status.KeyInvalid", (await ProblemAsync(malformed)).Code);

        var body = new { name = "اسم", color = "#112233", category = "active" };
        var changedKey = await SendAsync(admin, HttpMethod.Put, $"/api/v1/statuses/{id}", new { body.name, body.color, body.category, key = "other_key" }, etag);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, changedKey.StatusCode);
        Assert.Equal("Status.KeyImmutable", (await ProblemAsync(changedKey)).Code);

        var changedScope = await SendAsync(admin, HttpMethod.Put, $"/api/v1/statuses/{id}", new { body.name, body.color, body.category, scope = "project" }, etag);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, changedScope.StatusCode);
        Assert.Equal("Status.ScopeImmutable", (await ProblemAsync(changedScope)).Code);

        var repeated = await SendAsync(admin, HttpMethod.Put, $"/api/v1/statuses/{id}", new { body.name, body.color, body.category, key = key.ToUpperInvariant(), scope = "work-item" }, etag);
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
    }

    [Fact]
    public async Task Scenario_5_R3_color_and_category_are_validated_and_nothing_can_be_deleted()
    {
        using var admin = await factory.AdminClientAsync();
        var (id, _) = await CreateStatusAsync(admin, NewKey("v"));

        var badColor = await admin.PostAsJsonAsync(Statuses, new { key = NewKey("c"), name = "x", color = "blue", category = "active", scope = "work-item" });
        var problem = await ProblemAsync(badColor);
        Assert.Equal(HttpStatusCode.BadRequest, badColor.StatusCode);
        Assert.Contains(problem.Root.GetProperty("errors").GetProperty("color")[0].GetString()!, c => c is >= '؀' and <= 'ۿ');

        var badCategory = await admin.PostAsJsonAsync(Statuses, new { key = NewKey("g"), name = "x", color = "#112233", category = "weird", scope = "work-item" });
        Assert.Equal(HttpStatusCode.BadRequest, badCategory.StatusCode);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await admin.DeleteAsync(new Uri($"/api/v1/statuses/{id}", UriKind.Relative))).StatusCode);
        var workflow = await WorkflowByKeyAsync(admin, "standard");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await admin.DeleteAsync(new Uri($"/api/v1/workflows/{workflow.GetProperty("id").GetGuid()}", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task Scenario_6_R11_the_initial_status_of_a_workflow_cannot_be_deactivated_but_others_can()
    {
        using var admin = await factory.AdminClientAsync();
        var pending = await StatusByKeyAsync(admin, "work-item", "pending");
        var (_, _, a, b) = await CreateWorkflowAsync(admin, NewKey("wf"));
        var (_, aTag) = await GetAsync(admin, $"/api/v1/statuses/{pending.GetProperty("id").GetGuid()}");

        var refused = await SendAsync(
            admin, HttpMethod.Put, $"/api/v1/statuses/{pending.GetProperty("id").GetGuid()}",
            new { name = "جديد", color = "#8a94a0", category = "not-started", active = false }, aTag);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Equal("Status.IsInitialStatus", (await ProblemAsync(refused)).Code);
        Assert.Contains("سير العمل القياسي", (await ProblemAsync(refused)).Root.GetProperty("detail").GetString());

        var (bBody, bTag) = await GetAsync(admin, $"/api/v1/statuses/{b}");
        var off = await SendAsync(
            admin, HttpMethod.Put, $"/api/v1/statuses/{b}",
            new { name = bBody.GetProperty("name").GetString(), color = "#112233", category = "active", active = false }, bTag);
        Assert.Equal(HttpStatusCode.OK, off.StatusCode);
        Assert.False((await ReadAsync(off)).GetProperty("active").GetBoolean());

        var activeOnly = await ReadAsync(await admin.GetAsync(new Uri("/api/v1/statuses?active=true", UriKind.Relative)));
        Assert.DoesNotContain(b, activeOnly.EnumerateArray().Select(s => s.GetProperty("id").GetGuid()));
        Assert.Contains(b, (await ReadAsync(await admin.GetAsync(Statuses))).EnumerateArray().Select(s => s.GetProperty("id").GetGuid()));
        Assert.NotEqual(Guid.Empty, a);
    }

    [Fact]
    public async Task Scenario_10_a_stale_etag_is_412_and_a_missing_if_match_is_428()
    {
        using var admin = await factory.AdminClientAsync();
        var (id, etag) = await CreateStatusAsync(admin, NewKey("et"));
        var body = new { name = "اسم", color = "#112233", category = "active" };

        var first = await SendAsync(admin, HttpMethod.Put, $"/api/v1/statuses/{id}", body, etag);
        var second = await SendAsync(admin, HttpMethod.Put, $"/api/v1/statuses/{id}", body, etag);
        var missing = await SendAsync(admin, HttpMethod.Put, $"/api/v1/statuses/{id}", body, null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, second.StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionRequired, missing.StatusCode);
    }

    [Fact]
    public async Task Scenario_11_every_change_is_in_the_history_newest_first_and_only_for_status_managers()
    {
        using var admin = await factory.AdminClientAsync();
        using var noura = await factory.SignedInClientAsync("noura", ApiFactory.DemoPassword);
        var (id, etag) = await CreateStatusAsync(admin, NewKey("hs"));
        await SendAsync(admin, HttpMethod.Put, $"/api/v1/statuses/{id}", new { name = "اسم مُعدَّل", color = "#112233", category = "blocked" }, etag);

        var history = await ReadAsync(await admin.GetAsync(new Uri($"/api/v1/statuses/{id}/history", UriKind.Relative)));

        var items = history.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(["update", "insert"], items.Select(i => i.GetProperty("action").GetString()));
        Assert.Equal("سارة العتيبي", items[0].GetProperty("userName").GetString());
        Assert.Equal("اسم مُعدَّل", items[0].GetProperty("after").GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await noura.GetAsync(new Uri($"/api/v1/statuses/{id}/history", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(new Uri($"/api/v1/statuses/{Guid.NewGuid()}/history", UriKind.Relative))).StatusCode);
    }
}
