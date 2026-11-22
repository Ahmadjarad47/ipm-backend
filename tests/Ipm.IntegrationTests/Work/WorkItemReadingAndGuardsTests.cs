using System.Net;
using System.Net.Http.Json;
using Ipm.IntegrationTests.Config;
using Ipm.IntegrationTests.Infrastructure;
using static Ipm.IntegrationTests.Identity.IdentityApi;
using static Ipm.IntegrationTests.Projects.ProjectApi;
using static Ipm.IntegrationTests.Work.WorkApi;

namespace Ipm.IntegrationTests.Work;

/// <summary>Scenarios 14 to 17 of docs/modules/05-work-items.md and the demo data: the board, the progress, the protections owed from parts 02 to 04, and the impact previews.</summary>
[Collection(ApiCollection.Name)]
public sealed class WorkItemReadingAndGuardsTests(ApiFactory factory)
{
    [Fact]
    public async Task Demo_data_the_erp_project_has_twelve_items_numbered_from_101_with_their_tree()
    {
        using var admin = await factory.AdminClientAsync();
        var erp = await ProjectByCodeAsync(admin, "ERP");
        var page = await ReadAsync(await admin.GetAsync(new Uri($"/api/v1/work-items?projectId={erp.GetProperty("id").GetGuid()}&pageSize=200&sort=ref", UriKind.Relative)));

        Assert.Equal(12, page.GetProperty("total").GetInt32());
        var items = page.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal("ERP-101", items[0].GetProperty("ref").GetString());
        var second = items.Single(i => i.GetProperty("ref").GetString() == "ERP-102");
        Assert.Equal(items[0].GetProperty("id").GetGuid(), second.GetProperty("parentId").GetGuid());

        // R13: the leaves are 3, 5, 6, 8, 9, 10, 11 and 12; two of them (3 and 11) are done.
        Assert.Equal(12, erp.GetProperty("workItemCount").GetInt32());
        var progress = erp.GetProperty("progress");
        Assert.Equal(25, progress.GetProperty("percent").GetInt32());
        Assert.Equal(2, progress.GetProperty("doneLeaves").GetInt32());
        Assert.Equal(8, progress.GetProperty("countedLeaves").GetInt32());
        Assert.Equal(2, erp.GetProperty("overdueCount").GetInt32());
    }

    [Fact]
    public async Task Scenario_14_R11_the_board_has_the_light_items_the_workflows_and_only_the_card_fields()
    {
        using var admin = await factory.AdminClientAsync();
        using var abdullah = await factory.SignedInClientAsync("abdullah", ApiFactory.DemoPassword);
        using var noura = await factory.SignedInClientAsync("noura", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        await CreateAsync(admin, project, "feature", "ميزة", fields: new { business_value = "high", target_release = "2027" });
        var (gone, goneEtag, _) = await CreateAsync(admin, project, "task", "ستؤرشف");
        await CreateAsync(admin, project, "task", "باقية");
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(admin, HttpMethod.Delete, $"/api/v1/work-items/{gone}", null, goneEtag)).StatusCode);

        var board = await ReadAsync(await abdullah.GetAsync(new Uri($"/api/v1/work-items/board?projectId={project}", UriKind.Relative)));
        var items = board.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        var feature = items.Single(i => i.GetProperty("title").GetString() == "ميزة");
        Assert.True(feature.GetProperty("fields").TryGetProperty("business_value", out _));
        Assert.False(feature.GetProperty("fields").TryGetProperty("target_release", out _));
        Assert.Equal(5, board.GetProperty("workflows").GetArrayLength());
        Assert.All(board.GetProperty("workflows").EnumerateArray(), w => Assert.True(w.GetProperty("statusIds").GetArrayLength() > 0));

        Assert.Equal(HttpStatusCode.NotFound, (await noura.GetAsync(new Uri($"/api/v1/work-items/board?projectId={project}", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task Scenario_15_R13_progress_counts_the_done_leaves_and_an_empty_project_has_no_percent()
    {
        using var admin = await factory.AdminClientAsync();
        var (project, _) = await NewProjectAsync(admin);
        var fahad = await UserIdAsync(admin, "fahad");

        var empty = (await GetProjectAsync(admin, project)).Body;
        Assert.Equal(0, empty.GetProperty("workItemCount").GetInt32());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, empty.GetProperty("progress").GetProperty("percent").ValueKind);

        var (feature, _, _) = await CreateAsync(admin, project, "feature", "الأب");
        var (done, _, _) = await CreateAsync(admin, project, "task", "منجزة", parentId: feature, assignees: [fahad]);
        await CreateAsync(admin, project, "task", "متأخرة", parentId: feature, dueDate: "2020-01-01");
        await MoveAsync(admin, done, "in_progress", "in_review", "completed");

        var body = (await GetProjectAsync(admin, project)).Body;
        Assert.Equal(3, body.GetProperty("workItemCount").GetInt32());
        Assert.Equal(1, body.GetProperty("overdueCount").GetInt32());
        Assert.Equal(50, body.GetProperty("progress").GetProperty("percent").GetInt32());
        Assert.Equal(1, body.GetProperty("progress").GetProperty("doneLeaves").GetInt32());
        Assert.Equal(2, body.GetProperty("progress").GetProperty("countedLeaves").GetInt32());
    }

    [Fact]
    public async Task The_list_filters_by_overdue_assignee_search_and_sorts_by_priority()
    {
        using var admin = await factory.AdminClientAsync();
        var erp = (await ProjectByCodeAsync(admin, "ERP")).GetProperty("id").GetGuid();
        var reem = await UserIdAsync(admin, "reem");

        async Task<List<string>> Refs(string query) =>
            (await ReadAsync(await admin.GetAsync(new Uri($"/api/v1/work-items?projectId={erp}&pageSize=200&{query}", UriKind.Relative))))
                .GetProperty("items").EnumerateArray().Select(i => i.GetProperty("ref").GetString()!).ToList();

        Assert.Equal(["ERP-105", "ERP-109"], (await Refs("overdue=true&sort=ref")).Order().ToList());
        Assert.Equal(["ERP-102", "ERP-107", "ERP-111"], (await Refs($"assigneeId={reem}&sort=ref")).Where(r => r != "ERP-104").ToList());
        Assert.Equal(["ERP-105"], await Refs("q=ERP-105"));
        Assert.Equal(["ERP-106", "ERP-108"], await Refs("unassigned=true&sort=ref"));
        Assert.Equal("ERP-105", (await Refs("sort=priority&desc=true"))[0]);
        Assert.Equal(["ERP-101", "ERP-102", "ERP-104", "ERP-107"], await Refs("hasChildren=true&sort=ref"));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(new Uri("/api/v1/work-items?priority=urgent", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task Scenario_16_R14_configuration_cannot_strand_existing_items()
    {
        using var admin = await factory.AdminClientAsync();
        var (workflowId, workflowEtag, statusA, statusB) = await WorkflowApi.CreateWorkflowAsync(admin, ConfigApi.NewKey("pw"));
        var (typeId, typeEtag) = await ConfigApi.CreateTypeAsync(admin, ConfigApi.NewKey("pt"));
        var bound = await SendAsync(admin, HttpMethod.Put, $"/api/v1/work-item-types/{typeId}", new { name = "نوع", color = "#112233", icon = "task", workflowId }, typeEtag);
        Assert.Equal(HttpStatusCode.OK, bound.StatusCode);

        var (project, _) = await NewProjectAsync(admin, "task");
        var task = await TypeIdAsync(admin, "task");
        var (_, projectEtag) = await GetProjectAsync(admin, project);
        var allowed = await SendAsync(
            admin, HttpMethod.Put, $"/api/v1/projects/{project}/work-item-types", new { allowedTypeIds = new[] { task, typeId }, defaultTypeId = task }, projectEtag);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);

        var created = await admin.PostAsJsonAsync(
            new Uri("/api/v1/work-items", UriKind.Relative), new { projectId = project, typeId, title = "في الحالة الثانية", assigneeIds = Array.Empty<Guid>() });
        var id = (await ReadAsync(created)).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await TransitionItemAsync(admin, id, statusB, created.Headers.ETag)).StatusCode);

        // A status with items cannot be switched off, nor taken out of its workflow.
        var (statusB1, statusEtag) = await GetAsync(admin, $"/api/v1/statuses/{statusB}");
        var deactivate = await SendAsync(
            admin, HttpMethod.Put, $"/api/v1/statuses/{statusB}",
            new { name = statusB1.GetProperty("name").GetString(), color = "#112233", category = "active", active = false }, statusEtag);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, deactivate.StatusCode);
        Assert.Equal("Status.InUse", (await ProblemAsync(deactivate)).Code);

        var removed = await SendAsync(
            admin, HttpMethod.Put, $"/api/v1/workflows/{workflowId}", WorkflowApi.WorkflowBody((await GetAsync(admin, $"/api/v1/workflows/{workflowId}")).Body.GetProperty("key").GetString()!, [statusA], statusA, []), workflowEtag);
        Assert.Equal("Workflow.StatusInUse", (await ProblemAsync(removed)).Code);

        // The type cannot go back to a workflow without that status, and neither can a project override.
        var standard = (await WorkflowApi.WorkflowByKeyAsync(admin, "standard")).GetProperty("id").GetGuid();
        var (_, typeNow) = await GetAsync(admin, $"/api/v1/work-item-types/{typeId}");
        var switched = await SendAsync(admin, HttpMethod.Put, $"/api/v1/work-item-types/{typeId}", new { name = "نوع", color = "#112233", icon = "task", workflowId = standard }, typeNow);
        Assert.Equal("WorkItemType.WorkflowChangeConflicts", (await ProblemAsync(switched)).Code);

        var (_, projectNow) = await GetProjectAsync(admin, project);
        var overridden = await SendAsync(admin, HttpMethod.Put, $"/api/v1/projects/{project}/workflow-overrides", new { overrides = new Dictionary<Guid, Guid> { [typeId] = standard } }, projectNow);
        Assert.Equal("Project.WorkflowOverrideConflicts", (await ProblemAsync(overridden)).Code);

        // A type with items cannot be taken off the project allowed list.
        var narrowed = await SendAsync(admin, HttpMethod.Put, $"/api/v1/projects/{project}/work-item-types", new { allowedTypeIds = new[] { task }, defaultTypeId = task }, projectNow);
        Assert.Equal("Project.TypesInUse", (await ProblemAsync(narrowed)).Code);
    }

    [Fact]
    public async Task Scenario_16_R17_removing_a_member_with_open_work_succeeds_and_warns()
    {
        using var admin = await factory.AdminClientAsync();
        var (project, code) = await NewProjectAsync(admin);
        var reem = await UserIdAsync(admin, "reem");
        var (_, _, item) = await CreateAsync(admin, project, assignees: [reem]);

        var manager = await RoleIdAsync(admin, "project_manager");
        var viewer = await RoleIdAsync(admin, "viewer");
        var (_, projectEtag) = await GetProjectAsync(admin, project);
        var removed = await PutMembersAsync(admin, project, MembersBody((await MyIdAsync(admin), manager), (await UserIdAsync(admin, "abdullah"), viewer)), projectEtag);
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);

        var warnings = (await ReadAsync(removed)).GetProperty("warnings");
        Assert.Equal(1, warnings.GetArrayLength());
        Assert.Equal(reem, warnings[0].GetProperty("userId").GetGuid());
        Assert.Equal(item.GetProperty("ref").GetString(), warnings[0].GetProperty("refs")[0].GetString());
        Assert.StartsWith(code, warnings[0].GetProperty("refs")[0].GetString());

        // The item is left as it was: still assigned to the former member.
        Assert.Equal(reem, (await GetItemAsync(admin, item.GetProperty("id").GetGuid())).Body.GetProperty("assigneeIds")[0].GetGuid());
    }

    [Fact]
    public async Task Scenario_17_R15_the_impact_previews_count_what_a_change_would_touch()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var task = await TypeIdAsync(admin, "task");
        var inProgress = await StatusIdAsync(admin, "in_progress");
        var pending = await StatusIdAsync(admin, "pending");

        var type = await ReadAsync(await admin.GetAsync(new Uri($"/api/v1/work-item-types/{task}/impact", UriKind.Relative)));
        Assert.True(type.GetProperty("items").GetInt32() >= 1);
        Assert.True(type.GetProperty("projects").GetInt32() >= 1);
        Assert.True(type.GetProperty("workflows").GetInt32() >= 1);
        Assert.True(type.GetProperty("asDefaultIn").GetArrayLength() >= 1);

        var status = await ReadAsync(await admin.GetAsync(new Uri($"/api/v1/statuses/{inProgress}/impact", UriKind.Relative)));
        Assert.True(status.GetProperty("items").GetInt32() >= 1);
        Assert.True(status.GetProperty("workflows").GetInt32() >= 2);
        var initial = await ReadAsync(await admin.GetAsync(new Uri($"/api/v1/statuses/{pending}/impact", UriKind.Relative)));
        Assert.True(initial.GetProperty("asDefaultIn").GetArrayLength() >= 2);

        Assert.Equal(HttpStatusCode.Forbidden, (await reem.GetAsync(new Uri($"/api/v1/work-item-types/{task}/impact", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reem.GetAsync(new Uri($"/api/v1/statuses/{inProgress}/impact", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(new Uri($"/api/v1/statuses/{Guid.NewGuid()}/impact", UriKind.Relative))).StatusCode);
    }
}
