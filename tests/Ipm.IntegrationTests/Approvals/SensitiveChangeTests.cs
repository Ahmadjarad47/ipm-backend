using System.Net;
using System.Net.Http.Json;
using Ipm.IntegrationTests.Infrastructure;
using static Ipm.IntegrationTests.Approvals.ApprovalApi;
using static Ipm.IntegrationTests.Identity.IdentityApi;
using static Ipm.IntegrationTests.Projects.ProjectApi;
using static Ipm.IntegrationTests.Work.WorkApi;

namespace Ipm.IntegrationTests.Approvals;

/// <summary>Scenarios 1 to 13 of docs/modules/06b-sensitive-changes.md: proposing, applying and refusing direct edits of protected fields.</summary>
[Collection(ApiCollection.Name)]
public sealed class SensitiveChangeTests(ApiFactory factory)
{
    private static readonly string[] ItemFields = ["title", "dueDate", "parentId", "priority", "assigneeIds", "startDate"];

    private static async Task<string[]> ErrorPathsAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        return (await ReadAsync(response)).GetProperty("errors").EnumerateObject().Select(e => e.Name).ToArray();
    }

    [Fact]
    public async Task Scenario_1_R1_a_member_proposes_values_and_the_request_keeps_them_with_the_frozen_current_values()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        using var abdullah = await factory.SignedInClientAsync("abdullah", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var (item, _, _) = await CreateAsync(admin, project, "task", "العنوان القديم", dueDate: "2027-01-10");
        var template = await SensitiveTemplateAsync(admin, project, ItemFields);

        var response = await SubmitChangeAsync(reem, template, "work-item", item, new { title = "العنوان الجديد", dueDate = "2027-02-20" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal("change", body.GetProperty("action").GetString());
        Assert.Equal("pending-approval", body.GetProperty("status").GetString());
        Assert.Equal("العنوان الجديد", body.GetProperty("proposedChanges").GetProperty("title").GetString());
        Assert.Equal("العنوان القديم", body.GetProperty("snapshot").GetProperty("title").GetString());
        Assert.Equal("العنوان القديم", (await GetItemAsync(admin, item)).Body.GetProperty("title").GetString());

        Assert.Equal(HttpStatusCode.Forbidden, (await SubmitChangeAsync(abdullah, template, "work-item", item, new { title = "x" })).StatusCode);
        var plain = await CreateTemplateAsync(admin, "work-item-approve", ManagerStages(await MyIdAsync(admin)), ["title"], new { projectIds = new[] { project } });
        Assert.Equal("Approval.ProposedChangesNotAllowed", (await ProblemAsync(await SubmitChangeAsync(reem, plain, "work-item", item, new { title = "x" }))).Code);
        Assert.Equal("Approval.ProposedChangesRequired", (await ProblemAsync(await SubmitChangeAsync(reem, template, "work-item", item, new { }))).Code);
        Assert.Equal(HttpStatusCode.Conflict, (await SubmitChangeAsync(reem, template, "work-item", item, new { title = "ثاني" })).StatusCode);
    }

    [Fact]
    public async Task Scenario_2_3_R2_R3_only_protected_fields_the_requester_may_edit_can_be_proposed()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var (item, _, _) = await CreateAsync(admin, project, "task", "مهمة");
        var template = await SensitiveTemplateAsync(admin, project, ["title", "assigneeIds", "relations"]);
        var fahad = await UserIdAsync(admin, "fahad");

        var notProtected = await SubmitChangeAsync(reem, template, "work-item", item, new { priority = "low" });
        Assert.Equal("Approval.FieldsNotProtected", (await ProblemAsync(notProtected)).Code);
        var watched = await SubmitChangeAsync(reem, template, "work-item", item, new { relations = "x" });
        Assert.Equal("Approval.FieldsNotProtected", (await ProblemAsync(watched)).Code);

        // A team member holds tasks.edit but not tasks.assign.
        Assert.Equal(HttpStatusCode.Forbidden, (await SubmitChangeAsync(reem, template, "work-item", item, new { assigneeIds = fahad.ToString() })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await SubmitChangeAsync(admin, template, "work-item", item, new { assigneeIds = fahad.ToString() })).StatusCode);
    }

    [Fact]
    public async Task Scenario_4_5_R4_R5_the_values_are_checked_on_the_live_data_and_must_differ_from_the_current_ones()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var (other, _) = await NewProjectAsync(admin);
        var (task, _, _) = await CreateAsync(admin, project, "task", "مهمة", dueDate: "2027-03-10");
        var (feature, _, _) = await CreateAsync(admin, project, "feature", "ميزة");
        var (foreign, _, _) = await CreateAsync(admin, other, "feature", "من مشروع ثاني");
        var template = await SensitiveTemplateAsync(admin, project, [.. ItemFields, "typeId"]);
        var noura = await UserIdAsync(admin, "noura");

        Assert.Contains("proposedChanges.title", await ErrorPathsAsync(await SubmitChangeAsync(admin, template, "work-item", task, new { title = "  " })));
        Assert.Contains("proposedChanges.priority", await ErrorPathsAsync(await SubmitChangeAsync(admin, template, "work-item", task, new { priority = "urgent" })));
        Assert.Contains("proposedChanges.dueDate", await ErrorPathsAsync(await SubmitChangeAsync(admin, template, "work-item", task, new { dueDate = "03/10/2027" })));
        Assert.Contains("proposedChanges.dueDate", await ErrorPathsAsync(await SubmitChangeAsync(admin, template, "work-item", task, new { startDate = "2027-04-01" })));
        Assert.Contains("proposedChanges.assigneeIds", await ErrorPathsAsync(await SubmitChangeAsync(admin, template, "work-item", task, new { assigneeIds = noura.ToString() })));
        Assert.Contains("proposedChanges.parentId", await ErrorPathsAsync(await SubmitChangeAsync(admin, template, "work-item", task, new { parentId = foreign.ToString() })));
        Assert.Contains("proposedChanges.parentId", await ErrorPathsAsync(await SubmitChangeAsync(admin, template, "work-item", feature, new { parentId = task.ToString() })));
        Assert.Contains("proposedChanges.typeId", await ErrorPathsAsync(await SubmitChangeAsync(admin, template, "work-item", task, new { typeId = Guid.NewGuid().ToString() })));

        var same = await SubmitChangeAsync(reem, template, "work-item", task, new { title = "مهمة", dueDate = "2027-03-10" });
        Assert.Equal("Approval.NoChange", (await ProblemAsync(same)).Code);
    }


    [Fact]
    public async Task Scenario_6_R6_a_middle_stage_changes_nothing_and_the_last_one_applies_and_spends_the_request()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var (feature, _, _) = await CreateAsync(admin, project, "feature", "الأب");
        var (item, _, _) = await CreateAsync(admin, project, "task", "قديم", dueDate: "2027-01-10");
        var template = await SensitiveTemplateAsync(admin, project, ItemFields);
        var other = await SensitiveTemplateAsync(admin, project, ["priority"], priority: 5);

        var created = await SubmitChangeAsync(reem, template, "work-item", item, new { title = "جديد", dueDate = "2027-05-05", parentId = feature.ToString() });
        var id = (await ReadAsync(created)).GetProperty("id").GetGuid();
        var rival = await SubmitChangeAsync(reem, other, "work-item", item, new { priority = "low" });
        var rivalId = (await ReadAsync(rival)).GetProperty("id").GetGuid();

        var (body, etag) = await GetApprovalAsync(admin, id);
        var first = await DecideAsync(admin, id, "approve", null, body.GetProperty("revision").GetInt32(), etag);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("قديم", (await GetItemAsync(admin, item)).Body.GetProperty("title").GetString());

        var last = await DecideAsync(admin, id, "approve", "اعتماد", 1, first.Headers.ETag);
        Assert.Equal(HttpStatusCode.OK, last.StatusCode);
        var done = await ReadAsync(last);
        Assert.Equal("approved", done.GetProperty("status").GetString());
        Assert.NotEqual(System.Text.Json.JsonValueKind.Null, done.GetProperty("consumedAt").ValueKind);
        Assert.Equal("جديد", done.GetProperty("snapshot").GetProperty("title").GetString());

        var applied = (await GetItemAsync(admin, item)).Body;
        Assert.Equal("جديد", applied.GetProperty("title").GetString());
        Assert.Equal("2027-05-05", applied.GetProperty("dueDate").GetString());
        Assert.Equal(feature, applied.GetProperty("parentId").GetGuid());

        // The rival request protects a field that did not change, so it stays pending.
        Assert.Equal("pending-approval", (await GetApprovalAsync(admin, rivalId)).Body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Scenario_8_R6_a_proposal_that_is_impossible_when_approved_fails_the_decision_and_stays_pending()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var (feature, featureEtag, _) = await CreateAsync(admin, project, "feature", "الأب");
        var (item, _, _) = await CreateAsync(admin, project, "task", "مهمة");
        var template = await CreateTemplateAsync(
            admin, "sensitive-change", [Stage(1, "مدير المشروع", "position", "project-manager")], ItemFields,
            new { projectIds = new[] { project }, actions = new[] { "change" }, priority = 30 });

        var created = await SubmitChangeAsync(reem, template, "work-item", item, new { parentId = feature.ToString() });
        var id = (await ReadAsync(created)).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(admin, HttpMethod.Delete, $"/api/v1/work-items/{feature}", null, featureEtag)).StatusCode);

        var (body, etag) = await GetApprovalAsync(admin, id);
        var decided = await DecideAsync(admin, id, "approve", null, body.GetProperty("revision").GetInt32(), etag);
        Assert.Equal(HttpStatusCode.BadRequest, decided.StatusCode);
        Assert.Equal("pending-approval", (await GetApprovalAsync(admin, id)).Body.GetProperty("status").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, (await GetItemAsync(admin, item)).Body.GetProperty("parentId").ValueKind);

        // A returned request that became impossible does not reopen either (R10).
        var returned = await DecideAsync(admin, id, "return", "أعد", body.GetProperty("revision").GetInt32(), (await GetApprovalAsync(admin, id)).ETag);
        Assert.Equal(HttpStatusCode.OK, returned.StatusCode);
        var resent = await SendAsync(reem, HttpMethod.Post, $"/api/v1/approvals/{id}/resubmit", null, returned.Headers.ETag);
        Assert.Equal(HttpStatusCode.BadRequest, resent.StatusCode);
    }

    [Fact]
    public async Task Scenario_9_10_R8_a_direct_edit_of_a_protected_value_is_refused_and_what_is_not_protected_goes_through()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var (item, _, _) = await CreateAsync(admin, project, "task", "مهمة محكومة");
        var (feature, _, _) = await CreateAsync(admin, project, "feature", "ميزة");
        var template = await SensitiveTemplateAsync(admin, project, ["title", "parentId", "priority"]);

        var (current, etag) = await GetItemAsync(reem, item);
        var refused = await PutItemAsync(reem, item, UpdateBody(current, title: "عنوان مباشر"), etag);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        var problem = await ReadAsync(refused);
        Assert.Equal("Approval.ChangeRequiresApproval", problem.GetProperty("code").GetString());
        Assert.Contains("title", problem.GetProperty("detail").GetString());

        // The same title, or a field the template does not protect, is allowed.
        Assert.Equal(HttpStatusCode.OK, (await PutItemAsync(reem, item, UpdateBody(current), etag)).StatusCode);
        var (again, againTag) = await GetItemAsync(reem, item);
        Assert.Equal(HttpStatusCode.OK, (await PutItemAsync(reem, item, new { title = again.GetProperty("title").GetString(), description = "وصف جديد", priority = "high", assigneeIds = Array.Empty<Guid>(), fields = again.GetProperty("fields") }, againTag)).StatusCode);

        var (_, parentTag) = await GetItemAsync(reem, item);
        var parent = await SendAsync(reem, HttpMethod.Put, $"/api/v1/work-items/{item}/parent", new { parentId = feature }, parentTag);
        Assert.Equal("Approval.ChangeRequiresApproval", (await ProblemAsync(parent)).Code);

        var bulk = await BulkAsync(reem, [item], new { kind = "priority", priority = "critical" });
        Assert.False(bulk[0].GetProperty("ok").GetBoolean());

        // The administrator holds audit.override, so the direct edit goes through.
        var (adminView, adminTag) = await GetItemAsync(admin, item);
        Assert.Equal(HttpStatusCode.OK, (await PutItemAsync(admin, item, UpdateBody(adminView, title: "تعديل المدير"), adminTag)).StatusCode);
    }

    [Fact]
    public async Task Scenario_9_R8_a_governed_project_refuses_direct_name_and_date_edits_from_its_manager()
    {
        using var admin = await factory.AdminClientAsync();
        using var khaled = await factory.SignedInClientAsync("khaled", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var (_, projectEtag) = await GetProjectAsync(admin, project);
        var manager = await RoleIdAsync(admin, "project_manager");
        var members = await PutMembersAsync(
            admin, project,
            MembersBody((await MyIdAsync(admin), manager), (await UserIdAsync(admin, "khaled"), manager)), projectEtag);
        Assert.Equal(HttpStatusCode.OK, members.StatusCode);
        await CreateTemplateAsync(
            admin, "sensitive-change", ManagerStages(await MyIdAsync(admin)), ["name", "endDate"], new { projectIds = new[] { project }, actions = new[] { "change" }, priority = 30 });

        var (current, tag) = await GetProjectAsync(khaled, project);
        var name = await PutProjectAsync(khaled, project, UpdateBody("اسم مباشر", start: current.GetProperty("startDate").GetString()!, end: current.GetProperty("endDate").GetString()!), tag);
        Assert.Equal("Approval.ChangeRequiresApproval", (await ProblemAsync(name)).Code);

        var description = await PutProjectAsync(
            khaled, project,
            UpdateBody(current.GetProperty("name").GetString()!, start: current.GetProperty("startDate").GetString()!, end: current.GetProperty("endDate").GetString()!), tag);
        Assert.Equal(HttpStatusCode.OK, description.StatusCode);
    }

    [Fact]
    public async Task Scenario_4_6_a_project_proposal_for_its_name_and_owner_is_applied_when_approved()
    {
        using var admin = await factory.AdminClientAsync();
        using var khaled = await factory.SignedInClientAsync("khaled", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var (_, projectEtag) = await GetProjectAsync(admin, project);
        var manager = await RoleIdAsync(admin, "project_manager");
        var khaledId = await UserIdAsync(admin, "khaled");
        Assert.Equal(HttpStatusCode.OK, (await PutMembersAsync(admin, project, MembersBody((await MyIdAsync(admin), manager), (khaledId, manager)), projectEtag)).StatusCode);
        var template = await CreateTemplateAsync(
            admin, "sensitive-change", [Stage(1, "المدير", "user", (await MyIdAsync(admin)).ToString())], ["name", "ownerId", "endDate"],
            new { projectIds = new[] { project }, actions = new[] { "change" }, priority = 30 });

        Assert.Equal("Approval.FieldsNotProtected", (await ProblemAsync(await SubmitChangeAsync(khaled, template, "project", project, new { description = "x" }))).Code);
        Assert.Contains("proposedChanges.ownerId", await ErrorPathsAsync(await SubmitChangeAsync(khaled, template, "project", project, new { ownerId = (await UserIdAsync(admin, "noura")).ToString() })));
        Assert.Contains("proposedChanges.endDate", await ErrorPathsAsync(await SubmitChangeAsync(khaled, template, "project", project, new { endDate = "2020-01-01" })));

        var created = await SubmitChangeAsync(khaled, template, "project", project, new { name = "الاسم المعتمد", ownerId = khaledId.ToString() });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await ReadAsync(created)).GetProperty("id").GetGuid();
        var (body, etag) = await GetApprovalAsync(admin, id);
        Assert.Equal(HttpStatusCode.OK, (await DecideAsync(admin, id, "approve", null, body.GetProperty("revision").GetInt32(), etag)).StatusCode);

        var applied = (await GetProjectAsync(admin, project)).Body;
        Assert.Equal("الاسم المعتمد", applied.GetProperty("name").GetString());
        Assert.Equal(khaledId, applied.GetProperty("ownerId").GetGuid());
    }

    [Fact]
    public async Task Scenario_12_R7_applying_supersedes_another_pending_request_that_protects_the_same_field()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var (item, _, _) = await CreateAsync(admin, project, "task", "مهمة");
        var first = await CreateTemplateAsync(
            admin, "sensitive-change", [Stage(1, "المدير", "user", (await MyIdAsync(admin)).ToString())], ["title"],
            new { projectIds = new[] { project }, actions = new[] { "change" }, priority = 30 });
        var second = await CreateTemplateAsync(
            admin, "sensitive-change", [Stage(1, "المدير", "user", (await MyIdAsync(admin)).ToString())], ["title", "priority"],
            new { projectIds = new[] { project }, actions = new[] { "change" }, priority = 20 });

        var a = (await ReadAsync(await SubmitChangeAsync(reem, first, "work-item", item, new { title = "من الأول" }))).GetProperty("id").GetGuid();
        var b = (await ReadAsync(await SubmitChangeAsync(reem, second, "work-item", item, new { title = "من الثاني" }))).GetProperty("id").GetGuid();

        var (body, etag) = await GetApprovalAsync(admin, a);
        Assert.Equal(HttpStatusCode.OK, (await DecideAsync(admin, a, "approve", null, body.GetProperty("revision").GetInt32(), etag)).StatusCode);

        Assert.Equal("approved", (await GetApprovalAsync(admin, a)).Body.GetProperty("status").GetString());
        Assert.Equal("superseded", (await GetApprovalAsync(admin, b)).Body.GetProperty("status").GetString());
        Assert.Equal("من الأول", (await GetItemAsync(admin, item)).Body.GetProperty("title").GetString());
    }
}
