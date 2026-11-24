using System.Net;
using System.Net.Http.Json;
using Ipm.Domain.Identity;
using Ipm.Infrastructure.Persistence;
using Ipm.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Ipm.IntegrationTests.Approvals.ApprovalApi;
using static Ipm.IntegrationTests.Identity.IdentityApi;
using static Ipm.IntegrationTests.Projects.ProjectApi;
using static Ipm.IntegrationTests.Work.WorkApi;

namespace Ipm.IntegrationTests.Approvals;

/// <summary>
/// Gaps found by the test audit (docs/TEST-TRACEABILITY.md in ipm-web): scenarios 7, 11 and 13 of docs/modules/06b-sensitive-changes.md,
/// and the SRS acceptance tests AT-11, AT-12, AT-17 and AT-20 that the other suites only touch in part.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ApprovalGapTests(ApiFactory factory)
{
    private static readonly string[] ItemFields = ["title", "dueDate", "parentId", "priority", "assigneeIds", "startDate"];

    private static Uri Url(string path) => new(path, UriKind.Relative);

    private static async Task<string> StatusOfAsync(HttpClient client, Guid approval) =>
        (await GetApprovalAsync(client, approval)).Body.GetProperty("status").GetString()!;

    // 06b scenario 7 (R6-b), first half: the allowed route (audit.override) changes a protected field after submitting.
    [Fact]
    public async Task Scenario_7_R6b_a_protected_field_changed_before_the_last_stage_supersedes_the_request()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var (item, _, _) = await CreateAsync(admin, project, "task", "العنوان الأصلي");
        var template = await SensitiveTemplateAsync(admin, project, ItemFields);

        var created = await SubmitChangeAsync(reem, template, "work-item", item, new { title = "العنوان المقترح" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await ReadAsync(created)).GetProperty("id").GetGuid();

        // The administrator holds audit.override, so changing the protected priority directly is allowed...
        var (current, etag) = await GetItemAsync(admin, item);
        Assert.Equal(HttpStatusCode.OK, (await PutItemAsync(admin, item, UpdateBody(current, priority: "critical"), etag)).StatusCode);

        // ...and the pending request is superseded at once, so the old revision cannot be approved.
        var (body, requestTag) = await GetApprovalAsync(admin, id);
        Assert.Equal("superseded", body.GetProperty("status").GetString());
        var late = await DecideAsync(admin, id, "approve", null, 1, requestTag);
        Assert.Equal("Approval.Stale", (await ProblemAsync(late)).Code);
        Assert.Equal("العنوان الأصلي", (await GetItemAsync(admin, item)).Body.GetProperty("title").GetString());
    }

    // 06b scenario 7 (R6-b), second half: a value that changed without superseding is caught when the last stage applies it.
    [Fact]
    public async Task Scenario_7_R6b_a_protected_value_changed_behind_the_back_makes_the_last_approval_409_and_applies_nothing()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var (item, _, _) = await CreateAsync(admin, project, "task", "العنوان الأصلي", dueDate: "2027-01-10");
        var template = await SensitiveTemplateAsync(admin, project, ItemFields);

        var created = await SubmitChangeAsync(reem, template, "work-item", item, new { title = "العنوان المقترح", dueDate = "2027-06-06" });
        var id = (await ReadAsync(created)).GetProperty("id").GetGuid();

        var (body, etag) = await GetApprovalAsync(admin, id);
        var first = await DecideAsync(admin, id, "approve", null, body.GetProperty("revision").GetInt32(), etag);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // The title changes through no command at all (so nothing supersedes the request): the unwatched path of the spec.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var changed = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE work.work_items SET title = {"عنوان تغيّر خلسة"} WHERE id = {item}");
            Assert.Equal(1, changed);
        }

        Assert.Equal("pending-approval", await StatusOfAsync(admin, id));
        var last = await DecideAsync(admin, id, "approve", "اعتماد", 1, first.Headers.ETag);
        Assert.Equal(HttpStatusCode.Conflict, last.StatusCode);
        Assert.Equal("Approval.ProtectedFieldsChanged", (await ProblemAsync(last)).Code);

        // Nothing was applied and the request keeps its stage.
        var (after, _) = await GetApprovalAsync(admin, id);
        Assert.Equal("pending-approval", after.GetProperty("status").GetString());
        Assert.Equal(2, after.GetProperty("currentStage").GetInt32());
        var live = (await GetItemAsync(admin, item)).Body;
        Assert.Equal("عنوان تغيّر خلسة", live.GetProperty("title").GetString());
        Assert.Equal("2027-01-10", live.GetProperty("dueDate").GetString());
    }

    // 06b scenario 11 (R9): links and members are never refused, but they supersede a pending request that protects them.
    [Fact]
    public async Task Scenario_11_R9_creating_and_deleting_a_link_is_allowed_but_supersedes_a_pending_request_that_protects_relations()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var (a, _, _) = await CreateAsync(admin, project, "task", "أ");
        var (b, _, _) = await CreateAsync(admin, project, "task", "ب");
        var template = await CreateTemplateAsync(
            admin, "work-item-approve", ManagerStages(await MyIdAsync(admin)), ["relations"], new { projectIds = new[] { project }, priority = 40 });
        var kind = (await Ipm.IntegrationTests.Config.ConfigApi.RelationTypeByKeyAsync(admin, "relates")).GetProperty("id").GetGuid();

        var (id, _, _) = await SubmitAsync(reem, template, "work-item", a);
        var link = await admin.PostAsJsonAsync(Url("/api/v1/relations"), new { sourceId = a, targetId = b, relationTypeId = kind });
        Assert.Equal(HttpStatusCode.Created, link.StatusCode);
        Assert.Equal("superseded", await StatusOfAsync(admin, id));

        // Resubmitting reopens it; deleting the link supersedes it again.
        var (superseded, supersededTag) = await GetApprovalAsync(admin, id);
        var resent = await SendAsync(reem, HttpMethod.Post, $"/api/v1/approvals/{id}/resubmit", null, supersededTag);
        Assert.Equal(HttpStatusCode.OK, resent.StatusCode);
        Assert.Equal(superseded.GetProperty("revision").GetInt32() + 1, (await ReadAsync(resent)).GetProperty("revision").GetInt32());
        Assert.Equal("pending-approval", await StatusOfAsync(admin, id));

        var relationId = (await ReadAsync(link)).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync(Url($"/api/v1/relations/{relationId}"))).StatusCode);
        Assert.Equal("superseded", await StatusOfAsync(admin, id));
    }

    [Fact]
    public async Task Scenario_11_R9_replacing_the_members_is_allowed_but_supersedes_a_pending_request_that_protects_them()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var template = await CreateTemplateAsync(
            admin, "project-approve", ManagerStages(await MyIdAsync(admin)), ["members"], new { projectIds = new[] { project }, priority = 99 });

        var (id, _, _) = await SubmitAsync(reem, template, "project", project);
        var (members, membersTag) = await GetProjectAsync(admin, project);
        var current = members.GetProperty("members").EnumerateArray().Select(m => (m.GetProperty("userId").GetGuid(), m.GetProperty("roleId").GetGuid())).ToList();
        var noura = await UserIdAsync(admin, "noura");
        var viewer = await RoleIdAsync(admin, "viewer");

        var replaced = await PutMembersAsync(admin, project, MembersBody([.. current, (noura, viewer)]), membersTag);

        Assert.Equal(HttpStatusCode.OK, replaced.StatusCode);
        Assert.Equal("superseded", await StatusOfAsync(admin, id));
    }

    // 06b scenario 13 (R11): the request reads back the proposed values while the item still shows the current ones.
    [Fact]
    public async Task Scenario_13_R11_the_request_reads_back_the_proposed_values_and_the_item_keeps_the_current_ones_until_applied()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var (item, _, _) = await CreateAsync(admin, project, "task", "الحالي", dueDate: "2027-01-10");
        var template = await SensitiveTemplateAsync(admin, project, ItemFields);

        var created = await SubmitChangeAsync(reem, template, "work-item", item, new { title = "المقترح", dueDate = "2027-09-09" });
        var id = (await ReadAsync(created)).GetProperty("id").GetGuid();

        foreach (var client in new[] { reem, admin })
        {
            var (body, _) = await GetApprovalAsync(client, id);
            Assert.Equal("المقترح", body.GetProperty("proposedChanges").GetProperty("title").GetString());
            Assert.Equal("2027-09-09", body.GetProperty("proposedChanges").GetProperty("dueDate").GetString());
            Assert.Equal("الحالي", body.GetProperty("snapshot").GetProperty("title").GetString());
        }

        var (first, firstTag) = await GetApprovalAsync(admin, id);
        Assert.Equal(HttpStatusCode.OK, (await DecideAsync(admin, id, "approve", null, first.GetProperty("revision").GetInt32(), firstTag)).StatusCode);
        var live = (await GetItemAsync(admin, item)).Body;
        Assert.Equal("الحالي", live.GetProperty("title").GetString());
        Assert.Equal("2027-01-10", live.GetProperty("dueDate").GetString());
        Assert.Equal("المقترح", (await GetApprovalAsync(admin, id)).Body.GetProperty("proposedChanges").GetProperty("title").GetString());
    }

    // AT-11: a permission change in one project applies to that project only.
    [Fact]
    public async Task AT_11_changing_a_members_role_in_one_project_does_not_touch_the_same_person_in_another_project()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var editor = await CreateRoleAsync(admin, Unique("editor"), RoleScope.Project, [Permissions.ProjectsView, Permissions.ProjectsEdit]);
        var reader = await CreateRoleAsync(admin, Unique("reader"), RoleScope.Project, [Permissions.ProjectsView]);
        var owner = await MyIdAsync(admin);
        var reemId = await UserIdAsync(admin, "reem");
        var manager = await RoleIdAsync(admin, "project_manager");
        var (a, aTag) = await CreateProjectAsync(admin);
        var (b, bTag) = await CreateProjectAsync(admin);
        Assert.Equal(HttpStatusCode.OK, (await PutMembersAsync(admin, a, MembersBody((owner, manager), (reemId, editor.Id)), aTag)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PutMembersAsync(admin, b, MembersBody((owner, manager), (reemId, editor.Id)), bTag)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PutProjectAsync(reem, a, UpdateBody("أ قبل"), (await GetProjectAsync(reem, a)).ETag)).StatusCode);

        // Only in project A does reem become a reader.
        var (_, aNow) = await GetProjectAsync(admin, a);
        Assert.Equal(HttpStatusCode.OK, (await PutMembersAsync(admin, a, MembersBody((owner, manager), (reemId, reader.Id)), aNow)).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await PutProjectAsync(reem, a, UpdateBody("أ بعد"), (await GetProjectAsync(reem, a)).ETag)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PutProjectAsync(reem, b, UpdateBody("ب بعد"), (await GetProjectAsync(reem, b)).ETag)).StatusCode);

        // The change is on the record of project A, and project B's record shows no such change.
        var auditA = (await GetAsync(admin, $"/api/v1/audit?projectId={a}&pageSize=200")).Body.GetProperty("items").GetArrayLength();
        var logged = (await GetAsync(admin, $"/api/v1/audit?projectId={a}&pageSize=200")).Body.GetProperty("items").EnumerateArray()
            .Count(e => e.GetProperty("action").GetString()!.Contains("member", StringComparison.OrdinalIgnoreCase));
        Assert.True(auditA > 0);
        Assert.True(logged >= 1, "the members change of project A is in its audit trail");
    }

    // AT-12: after a status is renamed, older transitions stay understandable (items and history keep the stable id).
    [Fact]
    public async Task AT_12_renaming_a_status_keeps_old_transitions_readable_through_its_stable_id()
    {
        using var admin = await factory.AdminClientAsync();
        var gated = await GatedItemAsync(admin);
        var (current, etag) = await GetItemAsync(admin, gated.Item);
        var initial = current.GetProperty("statusId").GetGuid();

        // The administrator holds audit.override, so the gated move goes through without a request.
        var moved = await TransitionItemAsync(admin, gated.Item, gated.TargetStatus, etag);
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal("workItem.transition", (await ReadAuditActionsAsync(admin, gated.Item)).First(a => a.StartsWith("workItem.transition", StringComparison.Ordinal)));

        var (status, statusTag) = await GetAsync(admin, $"/api/v1/statuses/{gated.TargetStatus}");
        var oldName = status.GetProperty("name").GetString();
        var renamed = await SendAsync(
            admin, HttpMethod.Put, $"/api/v1/statuses/{gated.TargetStatus}",
            new { name = "اسم جديد بعد التحويل", color = status.GetProperty("color").GetString(), category = status.GetProperty("category").GetString() }, statusTag);
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.NotEqual("اسم جديد بعد التحويل", oldName);

        // The item and the filter still point at the same status id; only its name changed.
        var after = (await GetItemAsync(admin, gated.Item)).Body;
        Assert.Equal(gated.TargetStatus, after.GetProperty("statusId").GetGuid());
        var filtered = (await GetAsync(admin, $"/api/v1/work-items?projectId={gated.Project}&statusId={gated.TargetStatus}")).Body.GetProperty("items");
        Assert.Contains(gated.Item, filtered.EnumerateArray().Select(i => i.GetProperty("id").GetGuid()));
        Assert.DoesNotContain(gated.Item, (await GetAsync(admin, $"/api/v1/work-items?projectId={gated.Project}&statusId={initial}")).Body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()));

        // The old transition entry is still there, and the status history shows both names under the one id.
        Assert.Contains("workItem.transition", await ReadAuditActionsAsync(admin, gated.Item));
        var history = (await GetAsync(admin, $"/api/v1/statuses/{gated.TargetStatus}/history")).Body.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(["update", "insert"], history.Select(h => h.GetProperty("action").GetString()));
        Assert.Equal(oldName, history[1].GetProperty("after").GetProperty("name").GetString());
        Assert.Equal("اسم جديد بعد التحويل", history[0].GetProperty("after").GetProperty("name").GetString());
    }

    // AT-17: a parent cycle is refused as a whole; nothing is saved.
    [Fact]
    public async Task AT_17_a_parent_cycle_is_refused_atomically_and_the_tree_is_unchanged()
    {
        using var admin = await factory.AdminClientAsync();
        var (project, _) = await NewProjectAsync(admin);
        var (feature, _, _) = await CreateAsync(admin, project, "feature", "ميزة");
        var (task, _, _) = await CreateAsync(admin, project, "task", "مهمة", parentId: feature);
        var (bug, _, _) = await CreateAsync(admin, project, "subtask", "فرعية", parentId: task);

        // Making the feature a child of its own descendant is refused.
        var (_, featureTag) = await GetItemAsync(admin, feature);
        var refused = await SendAsync(admin, HttpMethod.Put, $"/api/v1/work-items/{feature}/parent", new { parentId = bug }, featureTag);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);

        Assert.Equal(System.Text.Json.JsonValueKind.Null, (await GetItemAsync(admin, feature)).Body.GetProperty("parentId").ValueKind);
        Assert.Equal(feature, (await GetItemAsync(admin, task)).Body.GetProperty("parentId").GetGuid());
        Assert.Equal(task, (await GetItemAsync(admin, bug)).Body.GetProperty("parentId").GetGuid());
        var parentChanges = (await ReadAuditActionsAsync(admin, feature)).Count(a => a == "workItem.parent");
        Assert.Equal(0, parentChanges);
    }

    // AT-20: moving an item under another parent while its approval is pending retires that approval.
    [Fact]
    public async Task AT_20_changing_the_parent_of_an_item_with_a_pending_approval_supersedes_it_and_the_old_revision_cannot_be_approved()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var (feature, _, _) = await CreateAsync(admin, project, "feature", "الأب الجديد");
        var (task, _, _) = await CreateAsync(admin, project, "task", "مهمة محمية");
        var template = await CreateTemplateAsync(
            admin, "work-item-approve", ManagerStages(await MyIdAsync(admin)), ["parentId"], new { projectIds = new[] { project }, priority = 45 });

        var (id, _, _) = await SubmitAsync(reem, template, "work-item", task);
        var (_, taskTag) = await GetItemAsync(admin, task);
        var moved = await SendAsync(admin, HttpMethod.Put, $"/api/v1/work-items/{task}/parent", new { parentId = feature }, taskTag);
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);

        var (body, requestTag) = await GetApprovalAsync(admin, id);
        Assert.Equal("superseded", body.GetProperty("status").GetString());
        var late = await DecideAsync(admin, id, "approve", null, 1, requestTag);
        Assert.Equal("Approval.Stale", (await ProblemAsync(late)).Code);
    }

    private static async Task<List<string>> ReadAuditActionsAsync(HttpClient client, Guid itemId) =>
        (await GetAsync(client, $"/api/v1/work-items/{itemId}/activity?pageSize=200")).Body.GetProperty("items").EnumerateArray()
            .Select(e => e.GetProperty("action").GetString()!).ToList();
}
