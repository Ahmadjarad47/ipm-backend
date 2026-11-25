using System.Net;
using Ipm.IntegrationTests.Infrastructure;
using static Ipm.IntegrationTests.Approvals.ApprovalApi;
using static Ipm.IntegrationTests.Identity.IdentityApi;
using static Ipm.IntegrationTests.Projects.ProjectApi;
using static Ipm.IntegrationTests.Work.WorkApi;

namespace Ipm.IntegrationTests.Approvals;

/// <summary>
/// The decision on a project approval moves the project by itself (no manual move): approved → active, rejected → cancelled.
/// Each scenario is a user story of the project header menu: request approval, approve, reject, send back, override, close.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ProjectApprovalOutcomeTests(ApiFactory factory)
{
    private async Task<(HttpClient Admin, HttpClient Reem, Guid Project, Guid Template)> StartedAsync()
    {
        var admin = await factory.AdminClientAsync();
        var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var template = await CreateTemplateAsync(
            admin, "project-approve", ManagerStages(await MyIdAsync(admin)), ["name"], new { projectIds = new[] { project }, priority = 99 });
        return (admin, reem, project, template);
    }

    private static async Task<Guid> StatusOfAsync(HttpClient client, Guid project) => (await GetProjectAsync(client, project)).Body.GetProperty("statusId").GetGuid();

    [Fact]
    public async Task Scenario_1_a_new_project_is_waiting_to_start_until_its_approval_is_decided()
    {
        var (admin, reem, project, template) = await StartedAsync();
        using (admin)
        using (reem)
        {
            Assert.Equal(await ProjectStatusIdAsync(admin, "pending"), await StatusOfAsync(admin, project));
            var (id, _, _) = await SubmitAsync(reem, template, "project", project);

            Assert.Equal("pending-approval", (await GetApprovalAsync(admin, id)).Body.GetProperty("status").GetString());
            Assert.Equal(await ProjectStatusIdAsync(admin, "pending"), await StatusOfAsync(admin, project)); // submitting changes nothing
        }
    }

    [Fact]
    public async Task Scenario_2_only_the_last_stage_approval_activates_the_project_and_spends_the_approval()
    {
        var (admin, reem, project, template) = await StartedAsync();
        using (admin)
        using (reem)
        {
            var (id, _, _) = await SubmitAsync(reem, template, "project", project);

            var (body, etag) = await GetApprovalAsync(admin, id);
            Assert.Equal(HttpStatusCode.OK, (await DecideAsync(admin, id, "approve", null, body.GetProperty("revision").GetInt32(), etag)).StatusCode);
            Assert.Equal(await ProjectStatusIdAsync(admin, "pending"), await StatusOfAsync(admin, project)); // stage 1 of 2: still waiting

            await ApproveAllAsync(admin, id);
            Assert.Equal(await ProjectStatusIdAsync(admin, "active"), await StatusOfAsync(admin, project));
            var approved = (await GetApprovalAsync(admin, id)).Body;
            Assert.Equal("approved", approved.GetProperty("status").GetString());
            Assert.NotEqual(System.Text.Json.JsonValueKind.Null, approved.GetProperty("consumedAt").ValueKind);
            var history = await ReadAsync(await admin.GetAsync(new Uri($"/api/v1/projects/{project}/status-history", UriKind.Relative)));
            Assert.False(history.GetProperty("items")[0].GetProperty("approvalOverridden").GetBoolean());
        }
    }

    [Fact]
    public async Task Scenario_3_rejecting_the_approval_cancels_the_project_and_needs_a_comment()
    {
        var (admin, reem, project, template) = await StartedAsync();
        using (admin)
        using (reem)
        {
            var (id, _, _) = await SubmitAsync(reem, template, "project", project);
            var (body, etag) = await GetApprovalAsync(admin, id);
            var revision = body.GetProperty("revision").GetInt32();

            var noComment = await DecideAsync(admin, id, "reject", "", revision, etag);
            Assert.False(noComment.IsSuccessStatusCode); // refused, nothing moves
            Assert.Equal(await ProjectStatusIdAsync(admin, "pending"), await StatusOfAsync(admin, project));

            (body, etag) = await GetApprovalAsync(admin, id);
            Assert.Equal(HttpStatusCode.OK, (await DecideAsync(admin, id, "reject", "خارج الميزانية", revision, etag)).StatusCode);
            Assert.Equal(await ProjectStatusIdAsync(admin, "cancelled"), await StatusOfAsync(admin, project));
            Assert.Equal("rejected", (await GetApprovalAsync(admin, id)).Body.GetProperty("status").GetString());
        }
    }

    [Fact]
    public async Task Scenario_4_sending_the_request_back_for_changes_does_not_move_the_project()
    {
        var (admin, reem, project, template) = await StartedAsync();
        using (admin)
        using (reem)
        {
            var (id, _, _) = await SubmitAsync(reem, template, "project", project);
            var (body, etag) = await GetApprovalAsync(admin, id);

            Assert.Equal(HttpStatusCode.OK, (await DecideAsync(admin, id, "return", "أضف الوصف", body.GetProperty("revision").GetInt32(), etag)).StatusCode);
            Assert.Equal("returned", (await GetApprovalAsync(admin, id)).Body.GetProperty("status").GetString());
            Assert.Equal(await ProjectStatusIdAsync(admin, "pending"), await StatusOfAsync(admin, project));
        }
    }

    [Fact]
    public async Task Scenario_5_an_administrator_override_decides_at_once_and_is_marked_in_the_history()
    {
        var (admin, reem, project, template) = await StartedAsync();
        using (admin)
        using (reem)
        {
            var (id, _, _) = await SubmitAsync(reem, template, "project", project);
            var (body, etag) = await GetApprovalAsync(admin, id);

            Assert.Equal(HttpStatusCode.OK, (await OverrideAsync(admin, id, "approve", "قرار إداري", body.GetProperty("revision").GetInt32(), etag)).StatusCode);
            Assert.Equal(await ProjectStatusIdAsync(admin, "active"), await StatusOfAsync(admin, project));
            var history = await ReadAsync(await admin.GetAsync(new Uri($"/api/v1/projects/{project}/status-history", UriKind.Relative)));
            Assert.True(history.GetProperty("items")[0].GetProperty("approvalOverridden").GetBoolean());
        }
    }

    [Fact]
    public async Task Scenario_6_an_override_that_rejects_cancels_the_project()
    {
        var (admin, reem, project, template) = await StartedAsync();
        using (admin)
        using (reem)
        {
            var (id, _, _) = await SubmitAsync(reem, template, "project", project);
            var (body, etag) = await GetApprovalAsync(admin, id);

            Assert.Equal(HttpStatusCode.OK, (await OverrideAsync(admin, id, "reject", "غير مطلوب", body.GetProperty("revision").GetInt32(), etag)).StatusCode);
            Assert.Equal(await ProjectStatusIdAsync(admin, "cancelled"), await StatusOfAsync(admin, project));
        }
    }

    [Fact]
    public async Task Scenario_7_a_closing_approval_does_not_move_the_project_by_itself()
    {
        var (admin, reem, project, start) = await StartedAsync();
        using (admin)
        using (reem)
        {
            var (first, _, _) = await SubmitAsync(reem, start, "project", project);
            await ApproveAllAsync(admin, first); // active now

            var close = await CreateTemplateAsync(admin, "project-close", ManagerStages(await MyIdAsync(admin)), ["endDate"], new { projectIds = new[] { project }, priority = 98 });
            var (closing, _, _) = await SubmitAsync(reem, close, "project", project);
            await ApproveAllAsync(admin, closing);

            Assert.Equal(await ProjectStatusIdAsync(admin, "active"), await StatusOfAsync(admin, project)); // closing is a manual, approved move
            var (_, etag) = await GetProjectAsync(admin, project);
            Assert.Equal(HttpStatusCode.OK, (await TransitionAsync(admin, project, await ProjectStatusIdAsync(admin, "completed"), etag)).StatusCode);
        }
    }

    [Fact]
    public async Task Scenario_8_a_decision_never_overrides_a_project_that_was_already_moved()
    {
        var (admin, reem, project, template) = await StartedAsync();
        using (admin)
        using (reem)
        {
            var (id, _, _) = await SubmitAsync(reem, template, "project", project);
            var (_, etag) = await GetProjectAsync(admin, project);
            Assert.Equal(HttpStatusCode.OK, (await TransitionAsync(admin, project, await ProjectStatusIdAsync(admin, "cancelled"), etag)).StatusCode); // cancelled by hand meanwhile

            await ApproveAllAsync(admin, id);

            Assert.Equal(await ProjectStatusIdAsync(admin, "cancelled"), await StatusOfAsync(admin, project)); // the late approval left it alone
        }
    }
}
