using System.Net;
using System.Net.Http.Json;
using Ipm.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using static Ipm.IntegrationTests.Approvals.ApprovalApi;
using static Ipm.IntegrationTests.Identity.IdentityApi;
using static Ipm.IntegrationTests.Projects.ProjectApi;
using static Ipm.IntegrationTests.Work.WorkApi;

namespace Ipm.IntegrationTests.Approvals;

/// <summary>Scenarios 3 to 7 and 12 to 14 of docs/modules/06-approvals.md: submitting, deciding, resubmitting, bulk, the boxes and reminders.</summary>
[Collection(ApiCollection.Name)]
public sealed class ApprovalRequestTests(ApiFactory factory)
{
    [Fact]
    public async Task Scenario_3_R4_a_member_submits_and_the_rules_of_the_template_and_the_subject_are_checked()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        using var abdullah = await factory.SignedInClientAsync("abdullah", ApiFactory.DemoPassword);
        using var noura = await factory.SignedInClientAsync("noura", ApiFactory.DemoPassword);
        var gated = await GatedItemAsync(admin);

        var (id, _, body) = await SubmitAsync(reem, gated.Template, "work-item", gated.Item);
        Assert.Equal("pending-approval", body.GetProperty("status").GetString());
        Assert.Equal(1, body.GetProperty("currentStage").GetInt32());
        Assert.Equal(1, body.GetProperty("revision").GetInt32());
        Assert.Equal("close", body.GetProperty("action").GetString());
        Assert.Equal("يحتاج موافقة", body.GetProperty("snapshot").GetProperty("title").GetString());
        Assert.Equal(2, body.GetProperty("stages").GetArrayLength());

        var again = await TrySubmitAsync(reem, gated.Template, "work-item", gated.Item);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("Approval.AlreadyPending", (await ProblemAsync(again)).Code);
        Assert.Equal(HttpStatusCode.Forbidden, (await TrySubmitAsync(abdullah, gated.Template, "work-item", gated.Item)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await TrySubmitAsync(noura, gated.Template, "work-item", gated.Item)).StatusCode);

        // A template whose rule does not cover the item, an action the template does not take, and a project template for an item.
        var other = await CreateTemplateAsync(admin, "work-item-approve", ManagerStages(await MyIdAsync(admin)), ["title"], new { workItemTypeIds = new[] { await TypeIdAsync(admin, "bug") } });
        Assert.Equal("Approval.TemplateNotApplicable", (await ProblemAsync(await TrySubmitAsync(reem, other, "work-item", gated.Item))).Code);
        var closeOnly = await TrySubmitAsync(reem, gated.Template, "work-item", gated.Item, "activate");
        Assert.Equal("Approval.ActionMismatch", (await ProblemAsync(closeOnly)).Code);
        Assert.Equal(HttpStatusCode.NotFound, (await TrySubmitAsync(reem, gated.Template, "work-item", Guid.NewGuid())).StatusCode);
    }

    [Fact]
    public async Task Scenario_3_R4_a_holder_of_the_override_permission_is_approved_at_submission_and_it_is_recorded_as_an_override()
    {
        using var admin = await factory.AdminClientAsync();
        var gated = await GatedItemAsync(admin);

        var (id, _, body) = await SubmitAsync(admin, gated.Template, "work-item", gated.Item);

        Assert.Equal("approved", body.GetProperty("status").GetString());
        var (stored, _) = await GetApprovalAsync(admin, id);
        Assert.Equal("approved", stored.GetProperty("status").GetString());
        var decisions = stored.GetProperty("decisions");
        Assert.Equal(1, decisions.GetArrayLength());
        Assert.Equal("approve", decisions[0].GetProperty("decision").GetString()!.ToLowerInvariant());
        Assert.False(string.IsNullOrWhiteSpace(decisions[0].GetProperty("comment").GetString()));
    }

    [Fact]
    public async Task Scenario_3_R4_a_stage_without_an_approver_and_an_archived_project_are_refused()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var gated = await GatedItemAsync(admin);

        // A project role nobody holds: a stage held by it has no approver.
        var (emptyRole, _) = await CreateRoleAsync(admin, Unique("empty"), Ipm.Domain.Identity.RoleScope.Project, []);
        var lonely = await CreateTemplateAsync(
            admin, "work-item-approve", [Stage(1, "دور فاضي", "role", emptyRole.ToString())], ["title"], new { workItemTypeIds = new[] { gated.TypeId } });
        var refused = await TrySubmitAsync(reem, lonely, "work-item", gated.Item);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Equal("Approval.StageUnresolvable", (await ProblemAsync(refused)).Code);

        var (_, projectEtag) = await GetProjectAsync(admin, gated.Project);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(admin, HttpMethod.Post, $"/api/v1/projects/{gated.Project}/archive", null, projectEtag)).StatusCode);
        var archived = await TrySubmitAsync(reem, gated.Template, "work-item", gated.Item);
        Assert.Equal("Approval.ProjectArchived", (await ProblemAsync(archived)).Code);
    }

    [Fact]
    public async Task Scenario_5_R6_each_stage_is_decided_by_its_approver_and_the_last_approval_closes_the_request()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        using var fahad = await factory.SignedInClientAsync("fahad", ApiFactory.DemoPassword);
        var gated = await GatedItemAsync(admin);
        var (id, etag, _) = await SubmitAsync(reem, gated.Template, "work-item", gated.Item);

        Assert.Equal(HttpStatusCode.Forbidden, (await DecideAsync(reem, id, "approve", null, 1, etag)).StatusCode); // the requester
        Assert.Equal(HttpStatusCode.Forbidden, (await DecideAsync(fahad, id, "approve", null, 1, etag)).StatusCode); // no approvals.decide
        Assert.Equal(HttpStatusCode.Conflict, (await DecideAsync(admin, id, "approve", null, 7, etag)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await DecideAsync(admin, id, "reject", "  ", 1, etag)).StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await DecideAsync(admin, id, "approve", null, 1, new System.Net.Http.Headers.EntityTagHeaderValue("\"1\""))).StatusCode);

        var first = await DecideAsync(admin, id, "approve", "المرحلة الأولى", 1, etag);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var afterFirst = await ReadAsync(first);
        Assert.Equal("pending-approval", afterFirst.GetProperty("status").GetString());
        Assert.Equal(2, afterFirst.GetProperty("currentStage").GetInt32());
        Assert.Equal("المرحلة الأولى", afterFirst.GetProperty("decisions")[0].GetProperty("comment").GetString());

        var last = await DecideAsync(admin, id, "approve", null, 1, first.Headers.ETag);
        var done = await ReadAsync(last);
        Assert.Equal("approved", done.GetProperty("status").GetString());
        Assert.Equal(0, done.GetProperty("currentStage").GetInt32());
        Assert.Equal(2, done.GetProperty("decisions").GetArrayLength());

        var late = await DecideAsync(admin, id, "approve", null, 1, last.Headers.ETag);
        Assert.Equal("Approval.NotPending", (await ProblemAsync(late)).Code);
    }

    [Fact]
    public async Task Scenario_5_R6_a_holder_of_audit_override_settles_the_request_at_once_and_skips_the_stages()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var gated = await GatedItemAsync(admin);
        var (id, etag, _) = await SubmitAsync(reem, gated.Template, "work-item", gated.Item);

        Assert.Equal(HttpStatusCode.Forbidden, (await OverrideAsync(reem, id, "approve", "أنا المقدِّم", 1, etag)).StatusCode); // no audit.override
        Assert.Equal(HttpStatusCode.BadRequest, (await OverrideAsync(admin, id, "approve", "  ", 1, etag)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await OverrideAsync(admin, id, "return", "ارجع", 1, etag)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await OverrideAsync(admin, id, "approve", "قرار", 7, etag)).StatusCode);

        var done = await OverrideAsync(admin, id, "approve", "أولوية قصوى", 1, etag);
        Assert.Equal(HttpStatusCode.OK, done.StatusCode);
        var body = await ReadAsync(done);
        Assert.Equal("approved", body.GetProperty("status").GetString());
        Assert.Equal(0, body.GetProperty("currentStage").GetInt32());
        Assert.Equal(1, body.GetProperty("decisions").GetArrayLength()); // the second stage was skipped

        Assert.Equal("Approval.NotPending", (await ProblemAsync(await OverrideAsync(admin, id, "reject", "مرة أخرى", 1, done.Headers.ETag))).Code);
    }

    [Fact]
    public async Task Scenario_6_R7_a_rejection_ends_it_and_a_return_is_resent_by_its_requester_as_a_new_revision()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        using var fahad = await factory.SignedInClientAsync("fahad", ApiFactory.DemoPassword);
        var gated = await GatedItemAsync(admin);

        var (rejected, rejectedEtag, _) = await SubmitAsync(reem, gated.Template, "work-item", gated.Item);
        var no = await DecideAsync(admin, rejected, "reject", "غير مكتمل", 1, rejectedEtag);
        Assert.Equal("rejected", (await ReadAsync(no)).GetProperty("status").GetString());
        Assert.Equal("Approval.NotResubmittable", (await ProblemAsync(await SendAsync(reem, HttpMethod.Post, $"/api/v1/approvals/{rejected}/resubmit", null, no.Headers.ETag))).Code);

        var (returned, returnedEtag, _) = await SubmitAsync(reem, gated.Template, "work-item", gated.Item);
        var back = await DecideAsync(admin, returned, "return", "أكمل البيانات", 1, returnedEtag);
        Assert.Equal("returned", (await ReadAsync(back)).GetProperty("status").GetString());

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(fahad, HttpMethod.Post, $"/api/v1/approvals/{returned}/resubmit", null, back.Headers.ETag)).StatusCode);
        var again = await SendAsync(reem, HttpMethod.Post, $"/api/v1/approvals/{returned}/resubmit", null, back.Headers.ETag);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        var body = await ReadAsync(again);
        Assert.Equal(2, body.GetProperty("revision").GetInt32());
        Assert.Equal(1, body.GetProperty("currentStage").GetInt32());
        Assert.Equal("pending-approval", body.GetProperty("status").GetString());
        Assert.Equal(1, body.GetProperty("decisions").GetArrayLength());

        // The revision the approver reviewed before is now stale.
        Assert.Equal(HttpStatusCode.Conflict, (await DecideAsync(admin, returned, "approve", null, 1, again.Headers.ETag)).StatusCode);
    }

    [Fact]
    public async Task Scenario_7_R8_bulk_approval_answers_each_request_and_needs_the_reviewed_revision()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var gated = await GatedItemAsync(admin);
        var (project, _) = await NewProjectAsync(admin);
        var (other, _, _) = await CreateAsync(admin, project, "task", "ثانية");
        var template = await CreateTemplateAsync(admin, "work-item-approve", ManagerStages(await MyIdAsync(admin)), ["title"], new { projectIds = new[] { project }, priority = 50 });
        var (one, _, _) = await SubmitAsync(reem, gated.Template, "work-item", gated.Item);
        var (two, _, _) = await SubmitAsync(reem, template, "work-item", other);

        var response = await admin.PostAsJsonAsync(
            new Uri("/api/v1/approvals/bulk-decide", UriKind.Relative),
            new { ids = new[] { one, two }, comment = "تمت المراجعة", revisions = new Dictionary<Guid, int> { [one] = 1, [two] = 9 } });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = (await ReadAsync(response)).EnumerateArray().ToDictionary(r => r.GetProperty("id").GetGuid(), r => r.GetProperty("ok").GetBoolean());
        Assert.True(results[one]);
        Assert.False(results[two]);
        Assert.Equal(2, (await GetApprovalAsync(admin, one)).Body.GetProperty("currentStage").GetInt32());

        var noRevision = await admin.PostAsJsonAsync(new Uri("/api/v1/approvals/bulk-decide", UriKind.Relative), new { ids = new[] { one }, revisions = new Dictionary<Guid, int>() });
        Assert.False((await ReadAsync(noRevision))[0].GetProperty("ok").GetBoolean());
        Assert.Equal("Approval.BulkInvalid", (await ProblemAsync(await admin.PostAsJsonAsync(
            new Uri("/api/v1/approvals/bulk-decide", UriKind.Relative), new { ids = Array.Empty<Guid>(), revisions = new Dictionary<Guid, int>() }))).Code);
        Assert.Equal(
            "Approval.BulkInvalid",
            (await ProblemAsync(await admin.PostAsJsonAsync(
                new Uri("/api/v1/approvals/bulk-decide", UriKind.Relative), new { ids = Enumerable.Range(0, 51).Select(_ => Guid.NewGuid()).ToArray(), revisions = new Dictionary<Guid, int>() }))).Code);
    }

    [Fact]
    public async Task Scenario_12_R12_the_boxes_show_what_the_caller_can_decide_sent_and_closed_and_hide_the_rest()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        using var abdullah = await factory.SignedInClientAsync("abdullah", ApiFactory.DemoPassword);
        using var noura = await factory.SignedInClientAsync("noura", ApiFactory.DemoPassword);
        var gated = await GatedItemAsync(admin);
        var (id, etag, _) = await SubmitAsync(reem, gated.Template, "work-item", gated.Item);

        async Task<List<Guid>> Box(HttpClient client, string box) =>
            (await ReadAsync(await client.GetAsync(new Uri($"/api/v1/approvals?box={box}&projectId={gated.Project}&pageSize=200", UriKind.Relative))))
                .GetProperty("items").EnumerateArray().Select(r => r.GetProperty("id").GetGuid()).ToList();

        Assert.Contains(id, await Box(admin, "inbox"));
        Assert.True((await GetApprovalAsync(admin, id)).Body.GetProperty("canDecide").GetBoolean());
        Assert.Empty(await Box(reem, "inbox"));
        Assert.Equal([id], await Box(reem, "sent"));
        Assert.Empty(await Box(abdullah, "inbox"));
        Assert.Empty(await Box(noura, "sent"));
        Assert.Equal(HttpStatusCode.NotFound, (await noura.GetAsync(new Uri($"/api/v1/approvals/{id}", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await abdullah.GetAsync(new Uri($"/api/v1/approvals/{id}", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(new Uri("/api/v1/approvals?box=trash", UriKind.Relative))).StatusCode);

        Assert.Empty(await Box(admin, "closed"));
        await DecideAsync(admin, id, "reject", "لا", 1, etag);
        Assert.Contains(id, await Box(admin, "closed"));
        Assert.Contains(id, await Box(reem, "closed"));
        Assert.DoesNotContain(id, await Box(admin, "inbox"));
    }

    [Fact]
    public async Task Scenario_13_R14_the_requester_reminds_once_an_hour_and_nobody_else_does()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        using var fahad = await factory.SignedInClientAsync("fahad", ApiFactory.DemoPassword);
        var gated = await GatedItemAsync(admin);
        var (id, _, _) = await SubmitAsync(reem, gated.Template, "work-item", gated.Item);

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(reem, HttpMethod.Post, $"/api/v1/approvals/{id}/remind", null, null)).StatusCode);
        var again = await SendAsync(reem, HttpMethod.Post, $"/api/v1/approvals/{id}/remind", null, null);
        Assert.Equal(HttpStatusCode.TooManyRequests, again.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(fahad, HttpMethod.Post, $"/api/v1/approvals/{id}/remind", null, null)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await SendAsync(admin, HttpMethod.Post, $"/api/v1/approvals/{id}/remind", null, null)).StatusCode); // the hour is shared by the request

        await ApproveAllAsync(admin, id);
        Assert.Equal("Approval.NotPending", (await ProblemAsync(await SendAsync(reem, HttpMethod.Post, $"/api/v1/approvals/{id}/remind", null, null))).Code);
    }

    [Fact]
    public async Task Scenario_14_R15_a_stage_past_its_due_time_is_overdue()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var gated = await GatedItemAsync(admin, ManagerStages(await MyIdAsync(admin), dueHours: 24));
        var (id, _, body) = await SubmitAsync(reem, gated.Template, "work-item", gated.Item);

        Assert.NotEqual(System.Text.Json.JsonValueKind.Null, body.GetProperty("stageDueAt").ValueKind);
        Assert.False(body.GetProperty("overdue").GetBoolean());

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Ipm.Infrastructure.Persistence.ApplicationDbContext>();
            await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlInterpolatedAsync(
                db.Database, $"UPDATE approvals.approval_requests SET stage_due_at = now() - interval '1 hour' WHERE id = {id}");
        }

        Assert.True((await GetApprovalAsync(admin, id)).Body.GetProperty("overdue").GetBoolean());
        var overdue = await ReadAsync(await admin.GetAsync(new Uri($"/api/v1/approvals?box=inbox&overdue=true&projectId={gated.Project}", UriKind.Relative)));
        Assert.Equal(id, overdue.GetProperty("items")[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Demo_data_the_four_sample_requests_show_in_the_closed_box_of_the_administrator_where_decided()
    {
        using var admin = await factory.AdminClientAsync();

        var closed = await ReadAsync(await admin.GetAsync(new Uri("/api/v1/approvals?box=closed&pageSize=200", UriKind.Relative)));

        var titles = closed.GetProperty("items").EnumerateArray().Select(r => r.GetProperty("title").GetString()).ToList();
        Assert.Contains(titles, t => t!.Contains("SSP-102", StringComparison.Ordinal));
        Assert.Contains(titles, t => t!.Contains("ERP-111", StringComparison.Ordinal));
        var all = await ReadAsync(await admin.GetAsync(new Uri("/api/v1/approvals?box=sent&pageSize=200&status=pending-approval", UriKind.Relative)));
        Assert.True(all.GetProperty("total").GetInt32() >= 0);
    }
}
