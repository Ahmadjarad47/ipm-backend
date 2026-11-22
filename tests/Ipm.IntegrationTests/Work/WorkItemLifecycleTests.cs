using System.Net;
using System.Net.Http.Json;
using Ipm.IntegrationTests.Config;
using Ipm.IntegrationTests.Infrastructure;
using static Ipm.IntegrationTests.Identity.IdentityApi;
using static Ipm.IntegrationTests.Projects.ProjectApi;
using static Ipm.IntegrationTests.Work.WorkApi;

namespace Ipm.IntegrationTests.Work;

/// <summary>Scenarios 1 to 8 of docs/modules/05-work-items.md: reading, creating, numbering, assignees, fields, updating, parents and archiving.</summary>
[Collection(ApiCollection.Name)]
public sealed class WorkItemLifecycleTests(ApiFactory factory)
{
    [Fact]
    public async Task Scenario_1_members_read_items_and_everyone_else_gets_404_or_401()
    {
        using var admin = await factory.AdminClientAsync();
        using var abdullah = await factory.SignedInClientAsync("abdullah", ApiFactory.DemoPassword);
        using var noura = await factory.SignedInClientAsync("noura", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var (id, _, _) = await CreateAsync(admin, project);

        var viewerRead = await abdullah.GetAsync(new Uri($"/api/v1/work-items/{id}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, viewerRead.StatusCode);
        Assert.NotNull(viewerRead.Headers.ETag);

        var listed = await ReadAsync(await abdullah.GetAsync(new Uri($"/api/v1/work-items?projectId={project}", UriKind.Relative)));
        Assert.Equal(1, listed.GetProperty("total").GetInt32());

        var outsider = await noura.GetAsync(new Uri($"/api/v1/work-items/{id}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.NotFound, outsider.StatusCode);
        Assert.Equal("WorkItem.NotFound", (await ProblemAsync(outsider)).Code);
        Assert.Equal(0, (await ReadAsync(await noura.GetAsync(new Uri($"/api/v1/work-items?projectId={project}", UriKind.Relative)))).GetProperty("total").GetInt32());

        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(new Uri($"/api/v1/work-items/{id}", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task Scenario_2_R1_creating_starts_in_the_initial_status_with_the_next_number()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        using var abdullah = await factory.SignedInClientAsync("abdullah", ApiFactory.DemoPassword);
        var (project, code) = await NewProjectAsync(admin);

        var (_, etag, body) = await CreateAsync(reem, project, "task", "مهمة جديدة");
        Assert.NotNull(etag);
        Assert.Equal($"{code}-101", body.GetProperty("ref").GetString());
        Assert.Equal(await StatusIdAsync(admin, "pending"), body.GetProperty("statusId").GetGuid());
        Assert.Equal("high", body.GetProperty("priority").GetString());
        Assert.Equal($"{code}-102", (await CreateAsync(reem, project)).Body.GetProperty("ref").GetString());

        var empty = await PostItemAsync(reem, project, title: "");
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);

        var viewer = await PostItemAsync(abdullah, project);
        Assert.Equal(HttpStatusCode.Forbidden, viewer.StatusCode);

        // A type that exists but is not allowed in this project.
        var (narrow, _) = await NewProjectAsync(admin, "task");
        var notAllowed = await PostItemAsync(admin, narrow, "bug");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, notAllowed.StatusCode);
        Assert.Equal("WorkItem.TypeNotAllowed", (await ProblemAsync(notAllowed)).Code);

        // An archived project is read-only.
        var (archivedProject, _) = await NewProjectAsync(admin);
        var (projectBody, projectEtag) = await GetProjectAsync(admin, archivedProject);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(admin, HttpMethod.Post, $"/api/v1/projects/{archivedProject}/archive", null, projectEtag)).StatusCode);
        Assert.False(projectBody.GetProperty("archived").GetBoolean());
        var inArchived = await PostItemAsync(admin, archivedProject);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, inArchived.StatusCode);
        Assert.Equal("WorkItem.ProjectArchived", (await ProblemAsync(inArchived)).Code);
    }

    [Fact]
    public async Task Scenario_3_R1_numbers_given_at_the_same_time_never_repeat()
    {
        using var admin = await factory.AdminClientAsync();
        var (project, _) = await NewProjectAsync(admin);

        var created = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => PostItemAsync(admin, project, title: $"متزامن {i}")));
        Assert.All(created, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));

        var numbers = new List<string>();
        foreach (var response in created)
        {
            numbers.Add((await ReadAsync(response)).GetProperty("ref").GetString()!);
        }

        Assert.Equal(8, numbers.Distinct().Count());
    }

    [Fact]
    public async Task Scenario_4_R4_assignees_need_tasks_assign_and_must_be_active_members()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var fahad = await UserIdAsync(admin, "fahad");
        var noura = await UserIdAsync(admin, "noura");
        var mona = await UserIdAsync(admin, "mona");

        var withoutAssign = await PostItemAsync(reem, project, assignees: [fahad]);
        Assert.Equal(HttpStatusCode.Forbidden, withoutAssign.StatusCode);

        var notMember = await PostItemAsync(admin, project, assignees: [noura]);
        Assert.Equal(HttpStatusCode.BadRequest, notMember.StatusCode);
        Assert.Equal("WorkItem.AssigneesInvalid", (await ProblemAsync(notMember)).Code);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostItemAsync(admin, project, assignees: [mona])).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostItemAsync(admin, project, assignees: [fahad, fahad])).StatusCode);

        var (_, _, body) = await CreateAsync(admin, project, assignees: [fahad]);
        Assert.Equal(fahad, body.GetProperty("assigneeIds")[0].GetGuid());
        Assert.Empty((await CreateAsync(reem, project)).Body.GetProperty("assigneeIds").EnumerateArray());
    }

    [Fact]
    public async Task Scenario_5_R5_custom_field_values_are_checked_against_the_type()
    {
        using var admin = await factory.AdminClientAsync();
        var (project, _) = await NewProjectAsync(admin, "task", "bug");

        var missing = await PostItemAsync(admin, project, "bug", fields: new { environment = "staging" });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Contains("fields.severity", (await ReadAsync(missing)).GetProperty("errors").EnumerateObject().Select(e => e.Name));

        Assert.Equal(HttpStatusCode.BadRequest, (await PostItemAsync(admin, project, "bug", fields: new { severity = "nope" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostItemAsync(admin, project, "task", fields: new { estimate_hours = 9000 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostItemAsync(admin, project, "task", fields: new { estimate_hours = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostItemAsync(admin, project, "task", fields: new { unknown_field = 1 })).StatusCode);

        var (_, _, ok) = await CreateAsync(admin, project, "bug", fields: new { severity = "major", reproducible = true });
        Assert.Equal("major", ok.GetProperty("fields").GetProperty("severity").GetString());
        Assert.True(ok.GetProperty("fields").GetProperty("reproducible").GetBoolean());
        var (_, _, hours) = await CreateAsync(admin, project, "task", fields: new { estimate_hours = 12.5 });
        Assert.Equal(12.5m, hours.GetProperty("fields").GetProperty("estimate_hours").GetDecimal());
    }

    [Fact]
    public async Task Scenario_6_R2_updating_checks_permissions_status_and_the_version()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        using var abdullah = await factory.SignedInClientAsync("abdullah", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var fahad = await UserIdAsync(admin, "fahad");
        var (id, etag, body) = await CreateAsync(reem, project);

        var changed = await PutItemAsync(reem, id, UpdateBody(body, title: "عنوان جديد", priority: "critical"), etag);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.NotEqual(etag, changed.Headers.ETag);
        Assert.Equal("عنوان جديد", (await ReadAsync(changed)).GetProperty("title").GetString());

        var stale = await PutItemAsync(reem, id, UpdateBody(body, title: "قديم"), etag);
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionRequired, (await PutItemAsync(reem, id, UpdateBody(body), null)).StatusCode);

        // Changing the assignees needs tasks.assign, which a team member does not hold; the viewer cannot edit at all.
        var assign = await PutItemAsync(reem, id, UpdateBody(body, assignees: [fahad]), changed.Headers.ETag);
        Assert.Equal(HttpStatusCode.Forbidden, assign.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PutItemAsync(abdullah, id, UpdateBody(body, title: "x"), changed.Headers.ETag)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PutItemAsync(admin, id, UpdateBody(body, assignees: [fahad]), changed.Headers.ETag)).StatusCode);

        // The status has its own operation.
        var (current, currentEtag) = await GetItemAsync(admin, id);
        var statusId = await StatusIdAsync(admin, "completed");
        var withStatus = await SendAsync(admin, HttpMethod.Put, $"/api/v1/work-items/{id}", new
        {
            title = "x", description = "", priority = "high", assigneeIds = new[] { fahad }, fields = current.GetProperty("fields"), statusId,
        }, currentEtag);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, withStatus.StatusCode);
        Assert.Equal("WorkItem.StatusChangeNotAllowed", (await ProblemAsync(withStatus)).Code);
    }

    [Fact]
    public async Task Scenario_7_R6_the_parent_is_in_the_same_project_allows_the_type_and_makes_no_cycle()
    {
        using var admin = await factory.AdminClientAsync();
        var (project, _) = await NewProjectAsync(admin);
        var (other, _) = await NewProjectAsync(admin);
        var (feature, featureEtag, _) = await CreateAsync(admin, project, "feature", "ميزة");
        var (task, taskEtag, _) = await CreateAsync(admin, project, "task", "مهمة", parentId: feature);

        Assert.Equal(feature, (await GetItemAsync(admin, task)).Body.GetProperty("parentId").GetGuid());

        // A feature under a task: the task type does not allow it.
        var wrongType = await SendAsync(admin, HttpMethod.Put, $"/api/v1/work-items/{feature}/parent", new { parentId = task }, featureEtag);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, wrongType.StatusCode);

        var (foreign, _, _) = await CreateAsync(admin, other, "feature", "ميزة أخرى");
        var differentProject = await SendAsync(admin, HttpMethod.Put, $"/api/v1/work-items/{task}/parent", new { parentId = foreign }, taskEtag);
        Assert.Equal("WorkItem.ParentDifferentProject", (await ProblemAsync(differentProject)).Code);

        // A cycle: the parent would be the item itself, or one of its descendants.
        var itself = await SendAsync(admin, HttpMethod.Put, $"/api/v1/work-items/{task}/parent", new { parentId = task }, taskEtag);
        Assert.Equal("WorkItem.ParentCycle", (await ProblemAsync(itself)).Code);
        var (subtask, _, _) = await CreateAsync(admin, project, "subtask", "فرعية", parentId: task);
        var (_, taskNow) = await GetItemAsync(admin, task);
        var below = await SendAsync(admin, HttpMethod.Put, $"/api/v1/work-items/{task}/parent", new { parentId = subtask }, taskNow);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, below.StatusCode);

        var cleared = await SendAsync(admin, HttpMethod.Put, $"/api/v1/work-items/{task}/parent", new { parentId = (Guid?)null }, taskNow);
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, (await ReadAsync(cleared)).GetProperty("parentId").ValueKind);
    }

    [Fact]
    public async Task Scenario_8_R3_archiving_keeps_the_item_and_refuses_active_children()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var (feature, _, _) = await CreateAsync(admin, project, "feature", "ميزة");
        var (task, _, _) = await CreateAsync(admin, project, "task", "مهمة", parentId: feature);

        var (_, featureEtag) = await GetItemAsync(admin, feature);
        var blocked = await SendAsync(admin, HttpMethod.Delete, $"/api/v1/work-items/{feature}", null, featureEtag);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, blocked.StatusCode);
        Assert.Equal("WorkItem.HasActiveChildren", (await ProblemAsync(blocked)).Code);

        var (_, taskEtag) = await GetItemAsync(admin, task);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(reem, HttpMethod.Delete, $"/api/v1/work-items/{task}", null, taskEtag)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(admin, HttpMethod.Delete, $"/api/v1/work-items/{task}", null, taskEtag)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(admin, HttpMethod.Delete, $"/api/v1/work-items/{feature}", null, featureEtag)).StatusCode);

        var defaultList = await ReadAsync(await admin.GetAsync(new Uri($"/api/v1/work-items?projectId={project}", UriKind.Relative)));
        Assert.Equal(0, defaultList.GetProperty("total").GetInt32());
        var all = await ReadAsync(await admin.GetAsync(new Uri($"/api/v1/work-items?projectId={project}&includeArchived=true", UriKind.Relative)));
        Assert.Equal(2, all.GetProperty("total").GetInt32());

        var (archived, archivedEtag) = await GetItemAsync(admin, task);
        Assert.True(archived.GetProperty("archived").GetBoolean());
        var edit = await PutItemAsync(admin, task, UpdateBody(archived, title: "بعد الأرشفة"), archivedEtag);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, edit.StatusCode);
        Assert.Equal("WorkItem.Archived", (await ProblemAsync(edit)).Code);
    }

    [Fact]
    public async Task Scenario_5_R5_a_required_custom_field_of_a_new_type_is_enforced_and_values_of_removed_fields_are_kept()
    {
        using var admin = await factory.AdminClientAsync();
        var key = ConfigApi.NewKey("ft");
        var (typeId, typeEtag) = await ConfigApi.CreateTypeAsync(admin, key);
        var fields = await SendAsync(admin, HttpMethod.Put, $"/api/v1/work-item-types/{typeId}/fields", new
        {
            fields = new object[]
            {
                new { key = "sev", label = "الخطورة", type = "select", required = true, options = new[] { new { value = "a", label = "أ" } }, showInList = false, showOnCard = true, order = 0 },
                new { key = "day", label = "اليوم", type = "date", required = false, showInList = false, showOnCard = false, order = 1 },
            },
        }, typeEtag);
        Assert.Equal(HttpStatusCode.OK, fields.StatusCode);

        var (project, _) = await NewProjectAsync(admin);
        var (_, projectEtag) = await GetProjectAsync(admin, project);
        var task = await TypeIdAsync(admin, "task");
        var allowed = await SendAsync(admin, HttpMethod.Put, $"/api/v1/projects/{project}/work-item-types", new { allowedTypeIds = new[] { task, typeId }, defaultTypeId = task }, projectEtag);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);

        async Task<HttpResponseMessage> Post(object values) => await admin.PostAsJsonAsync(
            new Uri("/api/v1/work-items", UriKind.Relative), new { projectId = project, typeId, title = "عنصر", assigneeIds = Array.Empty<Guid>(), fields = values });

        Assert.Equal(HttpStatusCode.BadRequest, (await Post(new { day = "2026-02-30" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(new { sev = "a", day = "10/10/2026" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Post(new { sev = "a", day = "2026-10-10" })).StatusCode);
    }
}
