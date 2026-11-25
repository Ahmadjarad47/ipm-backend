using System.Net;
using Ipm.IntegrationTests.Infrastructure;
using static Ipm.IntegrationTests.Approvals.ApprovalApi;
using static Ipm.IntegrationTests.Identity.IdentityApi;
using static Ipm.IntegrationTests.Projects.ProjectApi;
using static Ipm.IntegrationTests.Work.WorkApi;

namespace Ipm.IntegrationTests.Approvals;

/// <summary>Scenarios 8 to 10 of docs/modules/06-approvals.md: the approval gate on moves and the supersede of a pending request.</summary>
[Collection(ApiCollection.Name)]
public sealed class ApprovalGateTests(ApiFactory factory)
{
    [Fact]
    public async Task Scenario_9_R9_a_gated_item_move_spends_one_approved_request_once()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var gated = await GatedItemAsync(admin);

        var (_, etag) = await GetItemAsync(admin, gated.Item);
        var refused = await TransitionItemAsync(reem, gated.Item, gated.TargetStatus, etag);
        Assert.Equal("WorkItem.ApprovalRequired", (await ProblemAsync(refused)).Code);

        var (id, _, _) = await SubmitAsync(reem, gated.Template, "work-item", gated.Item);
        Assert.Equal("WorkItem.ApprovalRequired", (await ProblemAsync(await TransitionItemAsync(reem, gated.Item, gated.TargetStatus, etag))).Code); // still pending
        await ApproveAllAsync(admin, id);

        var moved = await TransitionItemAsync(reem, gated.Item, gated.TargetStatus, etag);
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        var history = await ReadAsync(await reem.GetAsync(new Uri($"/api/v1/work-items/{gated.Item}/status-history", UriKind.Relative)));
        Assert.False(history.GetProperty("items")[0].GetProperty("approvalOverridden").GetBoolean());
        Assert.NotEqual(System.Text.Json.JsonValueKind.Null, (await GetApprovalAsync(admin, id)).Body.GetProperty("consumedAt").ValueKind);

        // The approval is spent: the same move again (after reopening the status chain it would be needed) is refused for a member.
        var (_, again) = await GetItemAsync(admin, gated.Item);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await TransitionItemAsync(reem, gated.Item, gated.TargetStatus, again)).StatusCode);
    }

    [Fact]
    public async Task Scenario_10_R10_a_protected_field_changed_after_the_approval_stops_the_move()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var gated = await GatedItemAsync(admin);
        var (id, _, _) = await SubmitAsync(reem, gated.Template, "work-item", gated.Item);
        await ApproveAllAsync(admin, id);

        var (current, etag) = await GetItemAsync(admin, gated.Item);
        var edited = await PutItemAsync(admin, gated.Item, UpdateBody(current, title: "عنوان تغيّر بعد الموافقة"), etag);
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);

        var moved = await TransitionItemAsync(reem, gated.Item, gated.TargetStatus, edited.Headers.ETag);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, moved.StatusCode);
        Assert.Equal("Approval.ProtectedFieldsChanged", (await ProblemAsync(moved)).Code);
    }

    [Fact]
    public async Task Scenario_10_R11_changing_a_protected_field_of_a_pending_request_supersedes_it_and_resubmitting_reopens_it()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var gated = await GatedItemAsync(admin);
        var (id, _, _) = await SubmitAsync(reem, gated.Template, "work-item", gated.Item);

        var (current, etag) = await GetItemAsync(admin, gated.Item);
        // A field the template does not protect leaves the request alone.
        var plain = await PutItemAsync(admin, gated.Item, UpdateBody(current, priority: "low"), etag);
        Assert.Equal("pending-approval", (await GetApprovalAsync(admin, id)).Body.GetProperty("status").GetString());

        var title = await PutItemAsync(admin, gated.Item, UpdateBody(await ReadAsync(plain), title: "عنوان جديد"), plain.Headers.ETag);
        Assert.Equal(HttpStatusCode.OK, title.StatusCode);

        var (superseded, supersededEtag) = await GetApprovalAsync(admin, id);
        Assert.Equal("superseded", superseded.GetProperty("status").GetString());
        Assert.Equal(0, superseded.GetProperty("currentStage").GetInt32());

        var late = await DecideAsync(admin, id, "approve", null, 1, supersededEtag);
        Assert.Equal("Approval.Stale", (await ProblemAsync(late)).Code);

        var resent = await SendAsync(reem, HttpMethod.Post, $"/api/v1/approvals/{id}/resubmit", null, supersededEtag);
        Assert.Equal(HttpStatusCode.OK, resent.StatusCode);
        var body = await ReadAsync(resent);
        Assert.Equal("pending-approval", body.GetProperty("status").GetString());
        Assert.Equal(2, body.GetProperty("revision").GetInt32());
        Assert.Equal("عنوان جديد", body.GetProperty("snapshot").GetProperty("title").GetString());
    }

    [Fact]
    public async Task Scenario_8_R9_the_final_approval_of_a_project_activates_it_and_spends_the_approval()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var template = await CreateTemplateAsync(
            admin, "project-approve", ManagerStages(await MyIdAsync(admin)), ["name", "endDate"], new { projectIds = new[] { project }, priority = 99 });

        var (id, _, body) = await SubmitAsync(reem, template, "project", project);
        Assert.Equal("activate", body.GetProperty("action").GetString());
        await ApproveAllAsync(admin, id);

        // the project moved by itself: no manual transition is needed
        var active = await ProjectStatusIdAsync(admin, "active");
        Assert.Equal(active, (await GetProjectAsync(admin, project)).Body.GetProperty("statusId").GetGuid());
        Assert.NotEqual(System.Text.Json.JsonValueKind.Null, (await GetApprovalAsync(admin, id)).Body.GetProperty("consumedAt").ValueKind);
        var history = await ReadAsync(await admin.GetAsync(new Uri($"/api/v1/projects/{project}/status-history", UriKind.Relative)));
        Assert.False(history.GetProperty("items")[0].GetProperty("approvalOverridden").GetBoolean());
    }

    [Fact]
    public async Task Scenario_8_R4_a_project_is_approved_to_start_once_then_closed_while_running_and_approved_again_only_to_reopen()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var start = await CreateTemplateAsync(admin, "project-approve", ManagerStages(await MyIdAsync(admin)), ["name"], new { projectIds = new[] { project }, priority = 99 });
        var close = await CreateTemplateAsync(admin, "project-close", ManagerStages(await MyIdAsync(admin)), ["endDate"], new { projectIds = new[] { project }, priority = 98 });

        // not started: only the start approval, and only one while it is unspent
        Assert.Equal("Approval.ProjectNotRunning", (await ProblemAsync(await TrySubmitAsync(reem, close, "project", project))).Code);
        var (first, _, _) = await SubmitAsync(reem, start, "project", project);
        await ApproveAllAsync(admin, first);

        // the approval activated the project by itself: running, no start approval, only closing
        var (_, projectEtag) = await GetProjectAsync(admin, project);
        Assert.Equal("Approval.ProjectAlreadyRunning", (await ProblemAsync(await TrySubmitAsync(reem, start, "project", project))).Code);
        var (closing, _, _) = await SubmitAsync(reem, close, "project", project);
        await ApproveAllAsync(admin, closing);
        Assert.Equal("Approval.AlreadyApproved", (await ProblemAsync(await TrySubmitAsync(reem, close, "project", project))).Code);
        (_, projectEtag) = await GetProjectAsync(admin, project);
        Assert.Equal(HttpStatusCode.OK, (await TransitionAsync(admin, project, await ProjectStatusIdAsync(admin, "completed"), projectEtag)).StatusCode);

        // closed: no closing again, only a new start approval (reopening)
        Assert.Equal("Approval.ProjectNotRunning", (await ProblemAsync(await TrySubmitAsync(reem, close, "project", project))).Code);
        Assert.Equal(HttpStatusCode.Created, (await TrySubmitAsync(reem, start, "project", project)).StatusCode);
    }

    [Fact]
    public async Task Scenario_8_R9_the_override_still_works_without_a_request_and_is_recorded()
    {
        using var admin = await factory.AdminClientAsync();
        var (project, _) = await NewProjectAsync(admin);
        var active = await ProjectStatusIdAsync(admin, "active");
        var (_, projectEtag) = await GetProjectAsync(admin, project);

        var moved = await TransitionAsync(admin, project, active, projectEtag);

        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        var history = await ReadAsync(await admin.GetAsync(new Uri($"/api/v1/projects/{project}/status-history", UriKind.Relative)));
        Assert.True(history.GetProperty("items")[0].GetProperty("approvalOverridden").GetBoolean());
    }
}
