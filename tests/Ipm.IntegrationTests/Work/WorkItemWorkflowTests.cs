using System.Net;
using System.Net.Http.Json;
using Ipm.Domain.Identity;
using Ipm.IntegrationTests.Config;
using Ipm.IntegrationTests.Infrastructure;
using static Ipm.IntegrationTests.Identity.IdentityApi;
using static Ipm.IntegrationTests.Projects.ProjectApi;
using static Ipm.IntegrationTests.Work.WorkApi;

namespace Ipm.IntegrationTests.Work;

/// <summary>Scenarios 9 to 13 of docs/modules/05-work-items.md: transitions, closing and reopening, approvals, type changes and bulk changes.</summary>
[Collection(ApiCollection.Name)]
public sealed class WorkItemWorkflowTests(ApiFactory factory)
{
    [Fact]
    public async Task Scenario_9_R7_a_transition_follows_the_workflow_and_needs_an_assignee_where_it_says_so()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        using var abdullah = await factory.SignedInClientAsync("abdullah", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var fahad = await UserIdAsync(admin, "fahad");
        var (id, etag, body) = await CreateAsync(reem, project);
        var inProgress = await StatusIdAsync(admin, "in_progress");

        var noAssignee = await TransitionItemAsync(reem, id, inProgress, etag);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noAssignee.StatusCode);
        Assert.Equal("WorkItem.AssigneeRequired", (await ProblemAsync(noAssignee)).Code);

        var assigned = await PutItemAsync(admin, id, UpdateBody(body, assignees: [fahad]), etag);
        var moved = await TransitionItemAsync(reem, id, inProgress, assigned.Headers.ETag, "بدأ العمل");
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal(inProgress, (await ReadAsync(moved)).GetProperty("statusId").GetGuid());

        var history = await ReadAsync(await reem.GetAsync(new Uri($"/api/v1/work-items/{id}/status-history", UriKind.Relative)));
        Assert.Equal(1, history.GetProperty("total").GetInt32());
        var entry = history.GetProperty("items")[0];
        Assert.Equal("بدأ العمل", entry.GetProperty("comment").GetString());
        Assert.Equal(await StatusIdAsync(admin, "pending"), entry.GetProperty("fromStatusId").GetGuid());
        Assert.False(entry.GetProperty("approvalOverridden").GetBoolean());

        // Not defined by the workflow, and a viewer may not move anything.
        var undefined = await TransitionItemAsync(reem, id, await StatusIdAsync(admin, "completed"), moved.Headers.ETag);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, undefined.StatusCode);
        Assert.Equal("WorkItem.TransitionNotAllowed", (await ProblemAsync(undefined)).Code);
        Assert.Equal(HttpStatusCode.Forbidden, (await TransitionItemAsync(abdullah, id, await StatusIdAsync(admin, "in_review"), moved.Headers.ETag)).StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await TransitionItemAsync(reem, id, await StatusIdAsync(admin, "in_review"), etag)).StatusCode);
    }

    [Fact]
    public async Task Scenario_9_R7_an_extra_permission_of_the_transition_and_an_archived_project_are_checked()
    {
        using var admin = await factory.AdminClientAsync();
        var (project, _) = await NewProjectAsync(admin);

        // fahad may move items but not edit them: the way back from completed needs tasks.edit.
        var (role, _) = await CreateRoleAsync(
            admin, Unique("mover"), RoleScope.Project, [Permissions.ProjectsView, Permissions.TasksView, Permissions.WorkflowTransition]);
        var (_, projectEtag) = await GetProjectAsync(admin, project);
        var fahad = await UserIdAsync(admin, "fahad");
        var manager = await RoleIdAsync(admin, "project_manager");
        var members = await PutMembersAsync(admin, project, MembersBody((await MyIdAsync(admin), manager), (fahad, role)), projectEtag);
        Assert.Equal(HttpStatusCode.OK, members.StatusCode);
        using var mover = await factory.SignedInClientAsync("fahad", ApiFactory.DemoPassword);

        var (id, _, _) = await CreateAsync(admin, project, assignees: [fahad]);
        Assert.Equal(HttpStatusCode.OK, (await MoveAsync(mover, id, "in_progress", "in_review", "completed")).StatusCode);

        var (_, etag) = await GetItemAsync(admin, id);
        var reopen = await TransitionItemAsync(mover, id, await StatusIdAsync(admin, "in_progress"), etag);
        Assert.Equal(HttpStatusCode.Forbidden, reopen.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await TransitionItemAsync(admin, id, await StatusIdAsync(admin, "in_progress"), etag)).StatusCode);

        var (_, projectNow) = await GetProjectAsync(admin, project);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(admin, HttpMethod.Post, $"/api/v1/projects/{project}/archive", null, projectNow)).StatusCode);
        var (_, itemNow) = await GetItemAsync(admin, id);
        var inArchived = await TransitionItemAsync(admin, id, await StatusIdAsync(admin, "in_review"), itemNow);
        Assert.Equal("WorkItem.ProjectArchived", (await ProblemAsync(inArchived)).Code);
    }

    [Fact]
    public async Task Scenario_10_R9_closing_picks_the_done_status_and_reopening_needs_a_reason()
    {
        using var admin = await factory.AdminClientAsync();
        var (project, _) = await NewProjectAsync(admin);
        var fahad = await UserIdAsync(admin, "fahad");
        var (id, _, _) = await CreateAsync(admin, project, assignees: [fahad]);

        // From the initial status no done status is reachable, and nothing is closed yet to reopen.
        var (_, etag) = await GetItemAsync(admin, id);
        var early = await SendAsync(admin, HttpMethod.Post, $"/api/v1/work-items/{id}/close", new { }, etag);
        Assert.Equal("WorkItem.CloseTargetInvalid", (await ProblemAsync(early)).Code);
        var notClosed = await SendAsync(admin, HttpMethod.Post, $"/api/v1/work-items/{id}/reopen", new { comment = "سبب" }, etag);
        Assert.Equal("WorkItem.NotClosed", (await ProblemAsync(notClosed)).Code);

        await MoveAsync(admin, id, "in_progress", "in_review");
        var (_, review) = await GetItemAsync(admin, id);
        var closed = await SendAsync(admin, HttpMethod.Post, $"/api/v1/work-items/{id}/close", new { comment = "تم" }, review);
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        Assert.Equal(await StatusIdAsync(admin, "completed"), (await ReadAsync(closed)).GetProperty("statusId").GetGuid());

        var withoutReason = await SendAsync(admin, HttpMethod.Post, $"/api/v1/work-items/{id}/reopen", new { comment = "" }, closed.Headers.ETag);
        Assert.Equal(HttpStatusCode.BadRequest, withoutReason.StatusCode);
        var reopened = await SendAsync(admin, HttpMethod.Post, $"/api/v1/work-items/{id}/reopen", new { comment = "ظهر خطأ بعد الإغلاق" }, closed.Headers.ETag);
        Assert.Equal(HttpStatusCode.OK, reopened.StatusCode);
        Assert.Equal(await StatusIdAsync(admin, "in_progress"), (await ReadAsync(reopened)).GetProperty("statusId").GetGuid());

        var history = await ReadAsync(await admin.GetAsync(new Uri($"/api/v1/work-items/{id}/status-history", UriKind.Relative)));
        Assert.Equal(4, history.GetProperty("total").GetInt32());
        Assert.Equal("ظهر خطأ بعد الإغلاق", history.GetProperty("items")[0].GetProperty("comment").GetString());
    }

    [Fact]
    public async Task Scenario_11_R8_a_move_that_needs_an_approval_is_refused_unless_it_is_made_with_the_override()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);

        var (a, _) = await WorkflowApi.CreateStatusAsync(admin, ConfigApi.NewKey("pa"));
        var (b, _) = await WorkflowApi.CreateStatusAsync(admin, ConfigApi.NewKey("pb"));
        var workflow = await admin.PostAsJsonAsync(
            new Uri("/api/v1/workflows", UriKind.Relative),
            WorkflowApi.WorkflowBody(ConfigApi.NewKey("wf"), [a, b], a, [WorkflowApi.Move(a, b, approval: true)]));
        Assert.Equal(HttpStatusCode.Created, workflow.StatusCode);
        var workflowId = (await ReadAsync(workflow)).GetProperty("id").GetGuid();

        var (typeId, typeEtag) = await ConfigApi.CreateTypeAsync(admin, ConfigApi.NewKey("ap"));
        var bound = await SendAsync(admin, HttpMethod.Put, $"/api/v1/work-item-types/{typeId}", new { name = "نوع", color = "#112233", icon = "task", workflowId }, typeEtag);
        Assert.Equal(HttpStatusCode.OK, bound.StatusCode);

        var (project, _) = await NewProjectAsync(admin);
        var (_, projectEtag) = await GetProjectAsync(admin, project);
        var allowed = await SendAsync(
            admin, HttpMethod.Put, $"/api/v1/projects/{project}/work-item-types", new { allowedTypeIds = new[] { typeId }, defaultTypeId = typeId }, projectEtag);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);

        var created = await admin.PostAsJsonAsync(
            new Uri("/api/v1/work-items", UriKind.Relative), new { projectId = project, typeId, title = "يحتاج موافقة", assigneeIds = Array.Empty<Guid>() });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var item = await ReadAsync(created);
        Assert.Equal(a, item.GetProperty("statusId").GetGuid());
        var id = item.GetProperty("id").GetGuid();

        var refused = await TransitionItemAsync(reem, id, b, created.Headers.ETag);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Equal("WorkItem.ApprovalRequired", (await ProblemAsync(refused)).Code);

        var overridden = await TransitionItemAsync(admin, id, b, created.Headers.ETag, "تجاوز");
        Assert.Equal(HttpStatusCode.OK, overridden.StatusCode);
        var history = await ReadAsync(await admin.GetAsync(new Uri($"/api/v1/work-items/{id}/status-history", UriKind.Relative)));
        Assert.True(history.GetProperty("items")[0].GetProperty("approvalOverridden").GetBoolean());
    }

    [Fact]
    public async Task Scenario_12_R10_a_type_change_lists_every_conflict_and_changes_nothing_while_there_are_any()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var bug = await TypeIdAsync(admin, "bug");
        var subtask = await TypeIdAsync(admin, "subtask");
        var feature = await TypeIdAsync(admin, "feature");

        var (task, taskEtag, _) = await CreateAsync(admin, project, "task", "مهمة", fields: new { estimate_hours = 8 });

        // To a bug: the required severity is missing and the estimate would be lost.
        var check = await admin.PostAsJsonAsync(new Uri($"/api/v1/work-items/{task}/type-change/check", UriKind.Relative), new { typeId = bug });
        Assert.Equal(HttpStatusCode.OK, check.StatusCode);
        var fields = (await ReadAsync(check)).GetProperty("conflicts").EnumerateArray().Select(c => c.GetProperty("field").GetString()).ToList();
        Assert.Contains("fields.severity", fields);
        Assert.Contains("fields.estimate_hours", fields);

        var refused = await SendAsync(admin, HttpMethod.Post, $"/api/v1/work-items/{task}/type-change", new { typeId = bug }, taskEtag);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        var problem = await ReadAsync(refused);
        Assert.Equal("WorkItem.TypeChangeConflicts", problem.GetProperty("code").GetString());
        Assert.True(problem.GetProperty("conflicts").GetArrayLength() >= 2);
        Assert.Equal(await TypeIdAsync(admin, "task"), (await GetItemAsync(admin, task)).Body.GetProperty("typeId").GetGuid());

        // A subtask under the task cannot become a feature: the parent does not allow it and the feature field is missing.
        var (child, _, _) = await CreateAsync(admin, project, "subtask", "فرعية", parentId: task);
        var asFeature = await admin.PostAsJsonAsync(new Uri($"/api/v1/work-items/{child}/type-change/check", UriKind.Relative), new { typeId = feature });
        var childFields = (await ReadAsync(asFeature)).GetProperty("conflicts").EnumerateArray().Select(c => c.GetProperty("field").GetString()).ToList();
        Assert.Contains("parentId", childFields);
        var childTypeBlocks = await admin.PostAsJsonAsync(new Uri($"/api/v1/work-items/{task}/type-change/check", UriKind.Relative), new { typeId = feature });
        Assert.Contains("children", (await ReadAsync(childTypeBlocks)).GetProperty("conflicts").EnumerateArray().Select(c => c.GetProperty("field").GetString()));

        // A plain move works and the version moves; without the type permission it is refused.
        var (plain, plainEtag, _) = await CreateAsync(admin, project, "task", "بدون حقول");
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(reem, HttpMethod.Post, $"/api/v1/work-items/{plain}/type-change", new { typeId = subtask }, plainEtag)).StatusCode);
        var changed = await SendAsync(admin, HttpMethod.Post, $"/api/v1/work-items/{plain}/type-change", new { typeId = subtask }, plainEtag);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.NotEqual(plainEtag, changed.Headers.ETag);
        Assert.Equal(subtask, (await ReadAsync(changed)).GetProperty("typeId").GetGuid());
    }

    [Fact]
    public async Task Scenario_13_R12_a_bulk_change_reports_each_item_and_one_refusal_does_not_stop_the_rest()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var fahad = await UserIdAsync(admin, "fahad");
        var (one, _, _) = await CreateAsync(admin, project, title: "أ");
        var (two, _, _) = await CreateAsync(admin, project, title: "ب", assignees: [fahad]);

        var assign = await BulkAsync(admin, [one, two], new { kind = "assign", assigneeIds = new[] { fahad } });
        Assert.All(assign.EnumerateArray(), r => Assert.True(r.GetProperty("ok").GetBoolean()));

        var denied = await BulkAsync(reem, [one, two], new { kind = "assign", assigneeIds = Array.Empty<Guid>() });
        Assert.All(denied.EnumerateArray(), r => Assert.False(r.GetProperty("ok").GetBoolean()));

        var priority = await BulkAsync(reem, [one, two], new { kind = "priority", priority = "low" });
        Assert.All(priority.EnumerateArray(), r => Assert.True(r.GetProperty("ok").GetBoolean()));
        Assert.Equal("low", (await GetItemAsync(admin, one)).Body.GetProperty("priority").GetString());

        var due = await BulkAsync(admin, [one, two], new { kind = "dueDate", dueDate = "2027-01-15" });
        Assert.All(due.EnumerateArray(), r => Assert.True(r.GetProperty("ok").GetBoolean()));
        Assert.Equal("2027-01-15", (await GetItemAsync(admin, two)).Body.GetProperty("dueDate").GetString());

        // One item has no assignee any more, so only the other can start.
        var (three, _, _) = await CreateAsync(admin, project, title: "ج");
        var transition = await BulkAsync(reem, [three, two], new { kind = "transition", toStatusId = await StatusIdAsync(admin, "in_progress") });
        var results = transition.EnumerateArray().ToDictionary(r => r.GetProperty("id").GetGuid(), r => r.GetProperty("ok").GetBoolean());
        Assert.False(results[three]);
        Assert.True(results[two]);

        Assert.Equal("Bulk.Empty", (await ProblemAsync(await admin.PostAsJsonAsync(
            new Uri("/api/v1/work-items/bulk", UriKind.Relative), new { ids = Array.Empty<Guid>(), action = new { kind = "priority", priority = "low" } }))).Code);
        var tooMany = await admin.PostAsJsonAsync(
            new Uri("/api/v1/work-items/bulk", UriKind.Relative),
            new { ids = Enumerable.Range(0, 101).Select(_ => Guid.NewGuid()).ToArray(), action = new { kind = "priority", priority = "low" } });
        Assert.Equal("Bulk.TooMany", (await ProblemAsync(tooMany)).Code);
    }
}
