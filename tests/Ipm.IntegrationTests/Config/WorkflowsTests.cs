using System.Net;
using System.Net.Http.Json;
using Ipm.Domain.Identity;
using Ipm.IntegrationTests.Infrastructure;
using static Ipm.IntegrationTests.Config.ConfigApi;
using static Ipm.IntegrationTests.Config.WorkflowApi;
using static Ipm.IntegrationTests.Identity.IdentityApi;

namespace Ipm.IntegrationTests.Config;

/// <summary>Scenarios 1, 2, 4, 7–11 of docs/modules/03-workflows.md for workflows and their binding to work item types.</summary>
[Collection(ApiCollection.Name)]
public sealed class WorkflowsTests(ApiFactory factory)
{
    private static readonly Uri Workflows = new("/api/v1/workflows", UriKind.Relative);
    private static readonly Uri Types = new("/api/v1/work-item-types", UriKind.Relative);

    [Fact]
    public async Task Scenario_1_the_three_default_workflows_are_readable_with_their_transitions()
    {
        using var noura = await factory.SignedInClientAsync("noura", ApiFactory.DemoPassword);

        var standard = await WorkflowByKeyAsync(noura, "standard");
        var bug = await WorkflowByKeyAsync(noura, "bug");
        var project = await WorkflowByKeyAsync(noura, "project_default");

        Assert.Equal("work-item", standard.GetProperty("scope").GetString());
        Assert.Equal(6, standard.GetProperty("statusIds").GetArrayLength());
        Assert.Equal(9, standard.GetProperty("transitions").GetArrayLength());
        Assert.Equal(5, bug.GetProperty("statusIds").GetArrayLength());
        Assert.Equal(6, bug.GetProperty("transitions").GetArrayLength());
        Assert.Equal("project", project.GetProperty("scope").GetString());
        Assert.Equal(5, project.GetProperty("statusIds").GetArrayLength());

        var pending = await StatusByKeyAsync(noura, "work-item", "pending");
        Assert.Equal(pending.GetProperty("id").GetGuid(), standard.GetProperty("initialStatusId").GetGuid());

        // The two approval gates of the project workflow, and the assignee / dependency / permission conditions of work items.
        Assert.Equal(2, project.GetProperty("transitions").EnumerateArray().Count(t => t.GetProperty("requiresApproval").GetBoolean()));
        Assert.Contains(standard.GetProperty("transitions").EnumerateArray(), t => t.GetProperty("requiresAssignee").GetBoolean());
        Assert.Contains(standard.GetProperty("transitions").EnumerateArray(), t => t.GetProperty("blockedByOpenDependencies").GetBoolean());
        Assert.Contains(standard.GetProperty("transitions").EnumerateArray(), t => t.GetProperty("permission").GetString() == Permissions.TasksEdit);

        var projectsOnly = await ReadAsync(await noura.GetAsync(new Uri("/api/v1/workflows?scope=project", UriKind.Relative)));
        Assert.All(projectsOnly.EnumerateArray(), w => Assert.Equal("project", w.GetProperty("scope").GetString()));
        Assert.NotNull((await noura.GetAsync(new Uri($"/api/v1/workflows/{standard.GetProperty("id").GetGuid()}", UriKind.Relative))).Headers.ETag);
    }

    [Fact]
    public async Task Scenario_1_every_default_type_is_bound_to_its_workflow()
    {
        using var noura = await factory.SignedInClientAsync("noura", ApiFactory.DemoPassword);
        var standard = (await WorkflowByKeyAsync(noura, "standard")).GetProperty("id").GetGuid();
        var bug = (await WorkflowByKeyAsync(noura, "bug")).GetProperty("id").GetGuid();

        foreach (var key in new[] { "feature", "task", "subtask", "milestone" })
        {
            Assert.Equal(standard, (await TypeByKeyAsync(noura, key)).GetProperty("workflowId").GetGuid());
        }

        Assert.Equal(bug, (await TypeByKeyAsync(noura, "bug")).GetProperty("workflowId").GetGuid());
    }

    [Fact]
    public async Task Scenario_2_an_ordinary_user_cannot_create_a_workflow_but_the_administrator_can()
    {
        using var noura = await factory.SignedInClientAsync("noura", ApiFactory.DemoPassword);
        using var admin = await factory.AdminClientAsync();
        var (a, _) = await CreateStatusAsync(admin, NewKey("sa"));
        var (b, _) = await CreateStatusAsync(admin, NewKey("sb"));
        var body = WorkflowBody(NewKey("wf"), [a, b], a, [Move(a, b)]);

        Assert.Equal(HttpStatusCode.Forbidden, (await noura.PostAsJsonAsync(Workflows, body)).StatusCode);
        var created = await admin.PostAsJsonAsync(Workflows, body);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.ETag);
    }

    [Fact]
    public async Task Scenario_4_R5_R2_the_key_is_unique_ignoring_case_and_the_key_and_scope_never_change()
    {
        using var admin = await factory.AdminClientAsync();
        var key = NewKey("wk");
        var (id, etag, a, b) = await CreateWorkflowAsync(admin, key);

        var duplicate = await admin.PostAsJsonAsync(Workflows, WorkflowBody(key.ToUpperInvariant(), [a, b], a, [Move(a, b)]));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("Workflow.KeyTaken", (await ProblemAsync(duplicate)).Code);

        var malformed = await admin.PostAsJsonAsync(Workflows, WorkflowBody("1 bad", [a, b], a, [Move(a, b)]));
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Equal("Workflow.KeyInvalid", (await ProblemAsync(malformed)).Code);

        var changedKey = await SendAsync(admin, HttpMethod.Put, $"/api/v1/workflows/{id}", new { name = "x", statusIds = new[] { a, b }, initialStatusId = a, transitions = new[] { Move(a, b) }, key = "other_key" }, etag);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, changedKey.StatusCode);
        Assert.Equal("Workflow.KeyImmutable", (await ProblemAsync(changedKey)).Code);

        var changedScope = await SendAsync(admin, HttpMethod.Put, $"/api/v1/workflows/{id}", new { name = "x", statusIds = new[] { a, b }, initialStatusId = a, transitions = new[] { Move(a, b) }, scope = "project" }, etag);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, changedScope.StatusCode);
        Assert.Equal("Workflow.ScopeImmutable", (await ProblemAsync(changedScope)).Code);
    }

    [Fact]
    public async Task Scenario_7_R6_R7_R12_statuses_must_exist_share_the_scope_be_active_and_contain_the_initial_one()
    {
        using var admin = await factory.AdminClientAsync();
        var (a, _) = await CreateStatusAsync(admin, NewKey("sa"));
        var (b, _) = await CreateStatusAsync(admin, NewKey("sb"));
        var (projectStatus, _) = await CreateStatusAsync(admin, NewKey("sp"), "project");
        var (inactive, inactiveTag) = await CreateStatusAsync(admin, NewKey("si"));
        await SendAsync(admin, HttpMethod.Put, $"/api/v1/statuses/{inactive}", new { name = "معطّلة", color = "#112233", category = "active", active = false }, inactiveTag);

        var duplicated = await admin.PostAsJsonAsync(Workflows, WorkflowBody(NewKey("w"), [a, a], a, []));
        Assert.Equal(HttpStatusCode.BadRequest, duplicated.StatusCode);
        Assert.True((await ProblemAsync(duplicated)).Root.GetProperty("errors").TryGetProperty("statusIds", out _));

        var initialOutside = await admin.PostAsJsonAsync(Workflows, WorkflowBody(NewKey("w"), [a, b], Guid.NewGuid(), []));
        Assert.Equal(HttpStatusCode.BadRequest, initialOutside.StatusCode);
        Assert.True((await ProblemAsync(initialOutside)).Root.GetProperty("errors").TryGetProperty("initialStatusId", out _));

        foreach (var invalid in new[] { projectStatus, Guid.NewGuid(), inactive })
        {
            var response = await admin.PostAsJsonAsync(Workflows, WorkflowBody(NewKey("w"), [a, invalid], a, []));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("Workflow.StatusesInvalid", (await ProblemAsync(response)).Code);
        }

        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync(Workflows, WorkflowBody(NewKey("w"), [a, b], a, [Move(a, b)]))).StatusCode);
    }

    [Fact]
    public async Task Scenario_8_R8_R9_R10_each_transition_problem_is_reported_with_its_exact_path()
    {
        using var admin = await factory.AdminClientAsync();
        var (a, _) = await CreateStatusAsync(admin, NewKey("sa"));
        var (b, _) = await CreateStatusAsync(admin, NewKey("sb"));
        var outside = Guid.NewGuid();

        var response = await admin.PostAsJsonAsync(
            Workflows,
            WorkflowBody(NewKey("w"), [a, b], a, [Move(a, b), Move(a, b), Move(a, a), Move(a, outside), Move(b, a, permission: "made.up")]));
        var errors = (await ProblemAsync(response)).Root.GetProperty("errors");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(errors.TryGetProperty("transitions[1]", out _));
        Assert.True(errors.TryGetProperty("transitions[2].toStatusId", out _));
        Assert.True(errors.TryGetProperty("transitions[3].toStatusId", out _));
        Assert.True(errors.TryGetProperty("transitions[4].permission", out _));
    }

    [Fact]
    public async Task Scenario_8_R10_assignee_and_dependency_conditions_are_refused_in_a_project_workflow()
    {
        using var admin = await factory.AdminClientAsync();
        var (a, _) = await CreateStatusAsync(admin, NewKey("pa"), "project");
        var (b, _) = await CreateStatusAsync(admin, NewKey("pb"), "project");

        var refused = await admin.PostAsJsonAsync(
            Workflows, WorkflowBody(NewKey("w"), [a, b], a, [Move(a, b, assignee: true, dependencies: true)], scope: "project"));
        var errors = (await ProblemAsync(refused)).Root.GetProperty("errors");
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.True(errors.TryGetProperty("transitions[0].requiresAssignee", out _));
        Assert.True(errors.TryGetProperty("transitions[0].blockedByOpenDependencies", out _));

        var accepted = await admin.PostAsJsonAsync(
            Workflows, WorkflowBody(NewKey("w"), [a, b], a, [Move(a, b, approval: true, permission: Permissions.ProjectsEdit)], scope: "project"));
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
    }

    [Fact]
    public async Task A_workflow_update_replaces_the_statuses_and_transitions_and_keeps_a_deactivated_status_it_already_has()
    {
        using var admin = await factory.AdminClientAsync();
        var key = NewKey("up");
        var (id, etag, a, b) = await CreateWorkflowAsync(admin, key);
        var (c, _) = await CreateStatusAsync(admin, NewKey("sc"));

        var extended = await SendAsync(
            admin, HttpMethod.Put, $"/api/v1/workflows/{id}",
            new { name = "اسم جديد", statusIds = new[] { a, b, c }, initialStatusId = a, transitions = new[] { Move(a, b), Move(b, c, approval: true), Move(c, a, permission: Permissions.TasksEdit) } }, etag);
        Assert.Equal(HttpStatusCode.OK, extended.StatusCode);
        var body = await ReadAsync(extended);
        Assert.Equal("اسم جديد", body.GetProperty("name").GetString());
        Assert.Equal(3, body.GetProperty("transitions").GetArrayLength());
        Assert.Equal(key, body.GetProperty("key").GetString());

        // Deactivate b (not the initial status): it stays in the workflow that already has it, but no other workflow can take it.
        var (bBody, bTag) = await GetAsync(admin, $"/api/v1/statuses/{b}");
        await SendAsync(admin, HttpMethod.Put, $"/api/v1/statuses/{b}", new { name = bBody.GetProperty("name").GetString(), color = "#112233", category = "active", active = false }, bTag);

        var again = await SendAsync(
            admin, HttpMethod.Put, $"/api/v1/workflows/{id}",
            new { name = "اسم جديد", statusIds = new[] { a, b, c }, initialStatusId = a, transitions = new[] { Move(a, b) } }, extended.Headers.ETag);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);

        var other = await admin.PostAsJsonAsync(Workflows, WorkflowBody(NewKey("w"), [a, b], a, [Move(a, b)]));
        Assert.Equal(HttpStatusCode.BadRequest, other.StatusCode);
        Assert.Equal("Workflow.StatusesInvalid", (await ProblemAsync(other)).Code);
    }

    [Fact]
    public async Task Scenario_9_a_new_type_without_a_workflow_gets_the_standard_one()
    {
        using var admin = await factory.AdminClientAsync();
        var standard = (await WorkflowByKeyAsync(admin, "standard")).GetProperty("id").GetGuid();

        var (typeId, _) = await CreateTypeAsync(admin, NewKey("auto"));
        var (body, _) = await GetAsync(admin, $"/api/v1/work-item-types/{typeId}");

        Assert.Equal(standard, body.GetProperty("workflowId").GetGuid());
    }

    [Fact]
    public async Task Scenario_9_R13_a_type_can_only_be_bound_to_an_existing_work_item_workflow()
    {
        using var admin = await factory.AdminClientAsync();
        var project = (await WorkflowByKeyAsync(admin, "project_default")).GetProperty("id").GetGuid();
        var bug = (await WorkflowByKeyAsync(admin, "bug")).GetProperty("id").GetGuid();

        foreach (var invalid in new[] { project, Guid.NewGuid() })
        {
            var response = await admin.PostAsJsonAsync(Types, new { key = NewKey("bad"), name = "x", color = "#112233", icon = "task", workflowId = invalid });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("WorkItemType.WorkflowInvalid", (await ProblemAsync(response)).Code);
        }

        var created = await admin.PostAsJsonAsync(Types, new { key = NewKey("ok"), name = "x", color = "#112233", icon = "task", workflowId = bug });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(bug, (await ReadAsync(created)).GetProperty("workflowId").GetGuid());
    }

    [Fact]
    public async Task Scenario_9_updating_a_type_changes_its_workflow_and_omitting_it_keeps_the_current_one()
    {
        using var admin = await factory.AdminClientAsync();
        var bug = (await WorkflowByKeyAsync(admin, "bug")).GetProperty("id").GetGuid();
        var standard = (await WorkflowByKeyAsync(admin, "standard")).GetProperty("id").GetGuid();
        var (typeId, etag) = await CreateTypeAsync(admin, NewKey("move"));

        var moved = await SendAsync(admin, HttpMethod.Put, $"/api/v1/work-item-types/{typeId}", new { name = "نقل", color = "#112233", icon = "task", workflowId = bug }, etag);
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal(bug, (await ReadAsync(moved)).GetProperty("workflowId").GetGuid());

        var renamed = await SendAsync(admin, HttpMethod.Put, $"/api/v1/work-item-types/{typeId}", new { name = "اسم آخر", color = "#112233", icon = "task" }, moved.Headers.ETag);
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.Equal(bug, (await ReadAsync(renamed)).GetProperty("workflowId").GetGuid());
        Assert.NotEqual(standard, bug);

        var invalid = await SendAsync(admin, HttpMethod.Put, $"/api/v1/work-item-types/{typeId}", new { name = "x", color = "#112233", icon = "task", workflowId = Guid.NewGuid() }, renamed.Headers.ETag);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Scenario_10_a_stale_etag_is_412_and_a_missing_if_match_is_428()
    {
        using var admin = await factory.AdminClientAsync();
        var (id, etag, a, b) = await CreateWorkflowAsync(admin, NewKey("et"));
        var body = new { name = "اسم", statusIds = new[] { a, b }, initialStatusId = a, transitions = new[] { Move(a, b) } };

        var first = await SendAsync(admin, HttpMethod.Put, $"/api/v1/workflows/{id}", body, etag);
        var second = await SendAsync(admin, HttpMethod.Put, $"/api/v1/workflows/{id}", body, etag);
        var missing = await SendAsync(admin, HttpMethod.Put, $"/api/v1/workflows/{id}", body, null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, second.StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionRequired, missing.StatusCode);
    }

    [Fact]
    public async Task Scenario_11_every_change_is_in_the_history_newest_first_and_only_for_workflow_managers()
    {
        using var admin = await factory.AdminClientAsync();
        using var noura = await factory.SignedInClientAsync("noura", ApiFactory.DemoPassword);
        var (id, etag, a, b) = await CreateWorkflowAsync(admin, NewKey("hw"));
        await SendAsync(
            admin, HttpMethod.Put, $"/api/v1/workflows/{id}",
            new { name = "اسم مُعدَّل", statusIds = new[] { a, b }, initialStatusId = a, transitions = new[] { Move(a, b), Move(b, a) } }, etag);

        var history = await ReadAsync(await admin.GetAsync(new Uri($"/api/v1/workflows/{id}/history", UriKind.Relative)));

        var items = history.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(["update", "insert"], items.Select(i => i.GetProperty("action").GetString()));
        Assert.Equal("سارة العتيبي", items[0].GetProperty("userName").GetString());
        Assert.Equal("اسم مُعدَّل", items[0].GetProperty("after").GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await noura.GetAsync(new Uri($"/api/v1/workflows/{id}/history", UriKind.Relative))).StatusCode);
    }
}
