using System.Net;
using System.Net.Http.Json;
using Ipm.Domain.Identity;
using Ipm.IntegrationTests.Infrastructure;
using static Ipm.IntegrationTests.Identity.IdentityApi;
using static Ipm.IntegrationTests.Projects.ProjectApi;

namespace Ipm.IntegrationTests.Projects;

/// <summary>Scenarios 4, 5, 9, 10, 11 and 16 of docs/modules/04-projects.md: creating, editing, moving and archiving a project.</summary>
[Collection(ApiCollection.Name)]
public sealed class ProjectLifecycleTests(ApiFactory factory)
{
    private static readonly Uri Projects = new("/api/v1/projects", UriKind.Relative);

    [Fact]
    public async Task Scenario_4_R1_the_code_is_unique_ignoring_case_well_formed_and_never_changes()
    {
        using var admin = await factory.AdminClientAsync();
        var code = NewCode();
        var (id, etag) = await CreateProjectAsync(admin, code);
        var owner = await MyIdAsync(admin);

        var duplicate = await admin.PostAsJsonAsync(Projects, new { code = code.ToLowerInvariant(), name = "x", ownerId = owner, startDate = Start, endDate = End });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("Project.CodeTaken", (await ProblemAsync(duplicate)).Code);

        var malformed = await admin.PostAsJsonAsync(Projects, new { code = "1-bad", name = "x", ownerId = owner, startDate = Start, endDate = End });
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Equal("Project.CodeInvalid", (await ProblemAsync(malformed)).Code);

        var changed = await PutProjectAsync(admin, id, UpdateBody(code: "OTHER1"), etag);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, changed.StatusCode);
        Assert.Equal("Project.CodeImmutable", (await ProblemAsync(changed)).Code);

        var repeated = await PutProjectAsync(admin, id, UpdateBody(code: code.ToLowerInvariant()), etag);
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
    }

    [Fact]
    public async Task Scenario_5_a_new_project_starts_in_the_initial_status_with_the_owner_and_creator_as_managers()
    {
        using var khaled = await factory.SignedInClientAsync("khaled", ApiFactory.DemoPassword);
        using var fahad = await factory.SignedInClientAsync("fahad", ApiFactory.DemoPassword);
        var noura = await UserIdAsync(khaled, "noura");
        var khaledId = await MyIdAsync(khaled);
        var manager = await RoleIdAsync(khaled, "project_manager");
        var body = new { code = NewCode(), name = "مشروع جديد", ownerId = noura, startDate = Start, endDate = End };

        var denied = await fahad.PostAsJsonAsync(Projects, body);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var created = await khaled.PostAsJsonAsync(Projects, body);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.ETag);
        var project = await ReadAsync(created);

        Assert.Equal(await ProjectStatusIdAsync(khaled, "pending"), project.GetProperty("statusId").GetGuid());
        Assert.Equal(noura, project.GetProperty("ownerId").GetGuid());
        var members = project.GetProperty("members").EnumerateArray().Select(m => (m.GetProperty("userId").GetGuid(), m.GetProperty("roleId").GetGuid())).ToList();
        Assert.Equivalent(new[] { (noura, manager), (khaledId, manager) }, members);
        // Without a template every active type is allowed (other tests add types of their own, so the five defaults must be among them).
        var allowed = project.GetProperty("allowedTypeIds").EnumerateArray().Select(t => t.GetGuid()).ToList();
        foreach (var key in new[] { "feature", "task", "bug", "subtask", "milestone" })
        {
            Assert.Contains(await TypeIdAsync(khaled, key), allowed);
        }

        Assert.Equal(await TypeIdAsync(khaled, "task"), project.GetProperty("defaultTypeId").GetGuid());
        Assert.False(project.GetProperty("archived").GetBoolean());
    }

    [Fact]
    public async Task Scenario_5_R3_the_owner_must_be_an_active_existing_user()
    {
        using var admin = await factory.AdminClientAsync();
        var mona = await UserIdAsync(admin, "mona");

        foreach (var owner in new[] { mona, Guid.NewGuid() })
        {
            var response = await admin.PostAsJsonAsync(Projects, new { code = NewCode(), name = "x", ownerId = owner, startDate = Start, endDate = End });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("Project.OwnerInvalid", (await ProblemAsync(response)).Code);
        }
    }

    [Fact]
    public async Task Scenario_5_R2_the_name_and_the_dates_are_validated()
    {
        using var admin = await factory.AdminClientAsync();
        var owner = await MyIdAsync(admin);

        var noName = await admin.PostAsJsonAsync(Projects, new { code = NewCode(), name = "", ownerId = owner, startDate = Start, endDate = End });
        Assert.Equal(HttpStatusCode.BadRequest, noName.StatusCode);
        Assert.True((await ProblemAsync(noName)).Root.GetProperty("errors").TryGetProperty("name", out _));

        var backwards = await admin.PostAsJsonAsync(Projects, new { code = NewCode(), name = "x", ownerId = owner, startDate = End, endDate = Start });
        Assert.Equal(HttpStatusCode.BadRequest, backwards.StatusCode);
        Assert.Equal("Project.InvalidDates", (await ProblemAsync(backwards)).Code);
    }

    [Fact]
    public async Task Scenario_9_R9_the_status_changes_only_by_a_workflow_transition()
    {
        using var admin = await factory.AdminClientAsync();
        var (id, etag) = await CreateProjectAsync(admin);
        var completed = await ProjectStatusIdAsync(admin, "completed");
        var cancelled = await ProjectStatusIdAsync(admin, "cancelled");
        var pending = await ProjectStatusIdAsync(admin, "pending");

        var edit = await PutProjectAsync(admin, id, UpdateBody(statusId: completed), etag);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, edit.StatusCode);
        Assert.Equal("Project.StatusChangeNotAllowed", (await ProblemAsync(edit)).Code);

        var sameStatus = await PutProjectAsync(admin, id, UpdateBody(statusId: pending), etag);
        Assert.Equal(HttpStatusCode.OK, sameStatus.StatusCode);

        var notDefined = await TransitionAsync(admin, id, completed, sameStatus.Headers.ETag);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, notDefined.StatusCode);
        Assert.Equal("Project.TransitionNotAllowed", (await ProblemAsync(notDefined)).Code);

        var moved = await TransitionAsync(admin, id, cancelled, sameStatus.Headers.ETag, "تم الإلغاء بقرار الإدارة");
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal(cancelled, (await ReadAsync(moved)).GetProperty("statusId").GetGuid());

        var history = await ReadAsync(await admin.GetAsync(new Uri($"/api/v1/projects/{id}/status-history", UriKind.Relative)));
        var entry = history.GetProperty("items")[0];
        Assert.Equal(1, history.GetProperty("total").GetInt32());
        Assert.Equal(pending, entry.GetProperty("fromStatusId").GetGuid());
        Assert.Equal(cancelled, entry.GetProperty("toStatusId").GetGuid());
        Assert.Equal("سارة العتيبي", entry.GetProperty("actorName").GetString());
        Assert.Equal("تم الإلغاء بقرار الإدارة", entry.GetProperty("comment").GetString());
        Assert.False(entry.GetProperty("approvalOverridden").GetBoolean());

        var again = await TransitionAsync(admin, id, pending, moved.Headers.ETag);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, again.StatusCode);
    }

    [Fact]
    public async Task Scenario_10_R11_activation_needs_an_approval_so_only_audit_override_can_do_it_for_now()
    {
        using var admin = await factory.AdminClientAsync();
        using var khaled = await factory.SignedInClientAsync("khaled", ApiFactory.DemoPassword);
        var (id, _) = await CreateProjectAsync(khaled);
        var active = await ProjectStatusIdAsync(admin, "active");
        var (_, tag) = await GetProjectAsync(khaled, id);

        var refused = await TransitionAsync(khaled, id, active, tag);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Equal("Project.ApprovalRequired", (await ProblemAsync(refused)).Code);

        var (_, adminTag) = await GetProjectAsync(admin, id);
        var allowed = await TransitionAsync(admin, id, active, adminTag, "تفعيل بتجاوز الموافقة");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);

        var history = await ReadAsync(await admin.GetAsync(new Uri($"/api/v1/projects/{id}/status-history", UriKind.Relative)));
        Assert.True(history.GetProperty("items")[0].GetProperty("approvalOverridden").GetBoolean());
    }

    [Fact]
    public async Task Scenario_10_R10_a_transition_needs_projects_edit_in_the_project()
    {
        using var abdullah = await factory.SignedInClientAsync("abdullah", ApiFactory.DemoPassword);
        using var admin = await factory.AdminClientAsync();
        var erp = (await ProjectByCodeAsync(admin, "ERP")).GetProperty("id").GetGuid();
        var (_, tag) = await GetProjectAsync(abdullah, erp);

        var response = await TransitionAsync(abdullah, erp, await ProjectStatusIdAsync(admin, "on_hold"), tag);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Scenario_11_R6_an_archived_project_is_read_only_until_it_is_restored()
    {
        using var admin = await factory.AdminClientAsync();
        var (id, etag) = await CreateProjectAsync(admin);

        var archived = await SendAsync(admin, HttpMethod.Post, $"/api/v1/projects/{id}/archive", null, etag);
        Assert.Equal(HttpStatusCode.OK, archived.StatusCode);
        Assert.True((await ReadAsync(archived)).GetProperty("archived").GetBoolean());
        var tag = archived.Headers.ETag;

        var edit = await PutProjectAsync(admin, id, UpdateBody("جديد"), tag);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, edit.StatusCode);
        Assert.Equal("Project.Archived", (await ProblemAsync(edit)).Code);
        Assert.Equal("Project.Archived", (await ProblemAsync(await TransitionAsync(admin, id, await ProjectStatusIdAsync(admin, "cancelled"), tag))).Code);
        Assert.Equal("Project.Archived", (await ProblemAsync(await PutMembersAsync(admin, id, MembersBody(), tag))).Code);
        Assert.Equal(
            "Project.Archived",
            (await ProblemAsync(await SendAsync(admin, HttpMethod.Put, $"/api/v1/projects/{id}/work-item-types", new { allowedTypeIds = new[] { await TypeIdAsync(admin, "task") }, defaultTypeId = await TypeIdAsync(admin, "task") }, tag))).Code);

        var restored = await SendAsync(admin, HttpMethod.Post, $"/api/v1/projects/{id}/unarchive", null, tag);
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        Assert.False((await ReadAsync(restored)).GetProperty("archived").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await PutProjectAsync(admin, id, UpdateBody("جديد"), restored.Headers.ETag)).StatusCode);
    }

    [Fact]
    public async Task Scenario_11_archiving_needs_projects_archive_in_the_project()
    {
        using var noura = await factory.SignedInClientAsync("noura", ApiFactory.DemoPassword);
        using var admin = await factory.AdminClientAsync();
        var erp = (await ProjectByCodeAsync(admin, "ERP")).GetProperty("id").GetGuid();
        var (_, tag) = await GetProjectAsync(noura, erp);

        var response = await SendAsync(noura, HttpMethod.Post, $"/api/v1/projects/{erp}/archive", null, tag);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); // an approver may read the project but not archive it
    }

    [Fact]
    public async Task Scenario_16_a_stale_etag_is_412_and_a_missing_if_match_is_428()
    {
        using var admin = await factory.AdminClientAsync();
        var (id, etag) = await CreateProjectAsync(admin);

        var first = await PutProjectAsync(admin, id, UpdateBody("الأول"), etag);
        var second = await PutProjectAsync(admin, id, UpdateBody("الثاني"), etag);
        var missing = await PutProjectAsync(admin, id, UpdateBody("الثالث"), null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.NotEqual(etag, first.Headers.ETag);
        Assert.Equal(HttpStatusCode.PreconditionFailed, second.StatusCode);
        Assert.Equal("Concurrency.StaleVersion", (await ProblemAsync(second)).Code);
        Assert.Equal(HttpStatusCode.PreconditionRequired, missing.StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await TransitionAsync(admin, id, await ProjectStatusIdAsync(admin, "cancelled"), etag)).StatusCode);
    }
}
