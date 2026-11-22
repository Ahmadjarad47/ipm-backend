using System.Net;
using System.Net.Http.Json;
using Ipm.Domain.Identity;
using Ipm.IntegrationTests.Config;
using Ipm.IntegrationTests.Infrastructure;
using static Ipm.IntegrationTests.Identity.IdentityApi;
using static Ipm.IntegrationTests.Projects.ProjectApi;

namespace Ipm.IntegrationTests.Projects;

/// <summary>Scenarios 6, 12 and 13 of docs/modules/04-projects.md: allowed types, workflow overrides and the guards of earlier parts.</summary>
[Collection(ApiCollection.Name)]
public sealed class ProjectSettingsTests(ApiFactory factory)
{
    private static readonly Uri Projects = new("/api/v1/projects", UriKind.Relative);
    private static readonly Uri Templates = new("/api/v1/project-templates", UriKind.Relative);

    [Fact]
    public async Task Scenario_6_R5_a_template_fills_the_types_and_adds_the_valid_suggested_members()
    {
        using var admin = await factory.AdminClientAsync();
        var task = await TypeIdAsync(admin, "task");
        var bug = await TypeIdAsync(admin, "bug");
        var memberRole = await RoleIdAsync(admin, "team_member");
        var reem = await UserIdAsync(admin, "reem");
        var mona = await UserIdAsync(admin, "mona");
        var template = await admin.PostAsJsonAsync(
            Templates,
            new
            {
                name = Unique("قالب"),
                allowedTypeIds = new[] { task, bug },
                defaultTypeId = bug,
                suggestedRoleIds = new[] { memberRole },
                durationDays = 30,
                suggestedMembers = new[] { new { userId = (Guid?)reem, roleId = memberRole }, new { userId = (Guid?)mona, roleId = memberRole }, new { userId = (Guid?)null, roleId = memberRole } },
            });
        Assert.Equal(HttpStatusCode.Created, template.StatusCode);
        var templateId = (await ReadAsync(template)).GetProperty("id").GetGuid();

        var (id, _) = await CreateProjectAsync(admin, templateId: templateId);
        var (project, _) = await GetProjectAsync(admin, id);

        Assert.Equal([task, bug], project.GetProperty("allowedTypeIds").EnumerateArray().Select(t => t.GetGuid()));
        Assert.Equal(bug, project.GetProperty("defaultTypeId").GetGuid());
        var members = project.GetProperty("members").EnumerateArray().Select(m => m.GetProperty("userId").GetGuid()).ToList();
        Assert.Contains(reem, members);
        Assert.DoesNotContain(mona, members);
        Assert.Equal(2, members.Count);
    }

    [Fact]
    public async Task Scenario_6_R5_an_inactive_or_unknown_template_is_refused()
    {
        using var admin = await factory.AdminClientAsync();
        var task = await TypeIdAsync(admin, "task");
        var inactive = await admin.PostAsJsonAsync(
            Templates, new { name = Unique("معطّل"), active = false, allowedTypeIds = new[] { task }, defaultTypeId = task, suggestedRoleIds = Array.Empty<Guid>(), durationDays = 10 });
        var templateId = (await ReadAsync(inactive)).GetProperty("id").GetGuid();
        var owner = await MyIdAsync(admin);

        var refused = await admin.PostAsJsonAsync(Projects, new { code = NewCode(), name = "x", ownerId = owner, startDate = Start, endDate = End, templateId });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Equal("Project.TemplateInactive", (await ProblemAsync(refused)).Code);

        var unknown = await admin.PostAsJsonAsync(Projects, new { code = NewCode(), name = "x", ownerId = owner, startDate = Start, endDate = End, templateId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal("Project.TemplateNotFound", (await ProblemAsync(unknown)).Code);
    }

    [Fact]
    public async Task Scenario_12_R12_the_allowed_types_need_one_type_with_an_active_default_among_them()
    {
        using var admin = await factory.AdminClientAsync();
        var task = await TypeIdAsync(admin, "task");
        var bug = await TypeIdAsync(admin, "bug");
        var (inactiveType, typeTag) = await ConfigApi.CreateTypeAsync(admin, ConfigApi.NewKey("off"));
        await SendAsync(admin, HttpMethod.Put, $"/api/v1/work-item-types/{inactiveType}", new { name = "معطّل", color = "#112233", icon = "task", active = false }, typeTag);
        var (id, etag) = await CreateProjectAsync(admin);
        var url = $"/api/v1/projects/{id}/work-item-types";

        foreach (var body in new object[]
        {
            new { allowedTypeIds = Array.Empty<Guid>(), defaultTypeId = task },
            new { allowedTypeIds = new[] { task }, defaultTypeId = bug },
            new { allowedTypeIds = new[] { task, Guid.NewGuid() }, defaultTypeId = task },
            new { allowedTypeIds = new[] { task, inactiveType }, defaultTypeId = inactiveType },
        })
        {
            var response = await SendAsync(admin, HttpMethod.Put, url, body, etag);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Equal("Project.TypesInvalid", (await ProblemAsync(response)).Code);
        }

        var saved = await SendAsync(admin, HttpMethod.Put, url, new { allowedTypeIds = new[] { task, bug, task }, defaultTypeId = bug }, etag);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var project = await ReadAsync(saved);
        Assert.Equal([task, bug], project.GetProperty("allowedTypeIds").EnumerateArray().Select(t => t.GetGuid()));
        Assert.Equal(bug, project.GetProperty("defaultTypeId").GetGuid());
    }

    [Fact]
    public async Task Scenario_12_changing_the_allowed_types_needs_the_permission_in_the_project()
    {
        using var fahad = await factory.SignedInClientAsync("fahad", ApiFactory.DemoPassword);
        using var khaled = await factory.SignedInClientAsync("khaled", ApiFactory.DemoPassword);
        using var admin = await factory.AdminClientAsync();
        var task = await TypeIdAsync(admin, "task");
        var body = new { allowedTypeIds = new[] { task }, defaultTypeId = task };

        // A fresh project without items (the demo ERP project has items of every type, which part 05 protects, R14).
        var manager = await RoleIdAsync(admin, "project_manager");
        var member = await RoleIdAsync(admin, "team_member");
        var khaledId = await UserIdAsync(admin, "khaled");
        var (erp, created) = await CreateProjectAsync(admin, ownerId: khaledId);
        var seeded = await PutMembersAsync(admin, erp, MembersBody((khaledId, manager), (await UserIdAsync(admin, "fahad"), member)), created);
        Assert.Equal(HttpStatusCode.OK, seeded.StatusCode);
        var original = (await GetProjectAsync(admin, erp)).Body.GetProperty("allowedTypeIds").EnumerateArray().Select(t => t.GetGuid()).ToArray();

        var (_, fahadTag) = await GetProjectAsync(fahad, erp);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(fahad, HttpMethod.Put, $"/api/v1/projects/{erp}/work-item-types", body, fahadTag)).StatusCode);

        var (_, khaledTag) = await GetProjectAsync(khaled, erp);
        var narrowed = await SendAsync(khaled, HttpMethod.Put, $"/api/v1/projects/{erp}/work-item-types", body, khaledTag);
        Assert.Equal(HttpStatusCode.OK, narrowed.StatusCode);
        await SendAsync(khaled, HttpMethod.Put, $"/api/v1/projects/{erp}/work-item-types", new { allowedTypeIds = original, defaultTypeId = task }, narrowed.Headers.ETag);
    }

    [Fact]
    public async Task Scenario_12_R13_a_workflow_override_is_listed_and_the_types_own_workflow_removes_it()
    {
        using var admin = await factory.AdminClientAsync();
        var task = await TypeByKeyIdAsync(admin, "task");
        var standard = (await WorkflowApi.WorkflowByKeyAsync(admin, "standard")).GetProperty("id").GetGuid();
        var bug = (await WorkflowApi.WorkflowByKeyAsync(admin, "bug")).GetProperty("id").GetGuid();
        var projectWorkflow = (await WorkflowApi.WorkflowByKeyAsync(admin, "project_default")).GetProperty("id").GetGuid();
        var (id, etag) = await CreateProjectAsync(admin);
        var url = $"/api/v1/projects/{id}/workflow-overrides";

        foreach (var invalid in new object[]
        {
            new { overrides = new Dictionary<Guid, Guid> { [Guid.NewGuid()] = bug } },
            new { overrides = new Dictionary<Guid, Guid> { [task] = Guid.NewGuid() } },
            new { overrides = new Dictionary<Guid, Guid> { [task] = projectWorkflow } },
        })
        {
            var response = await SendAsync(admin, HttpMethod.Put, url, invalid, etag);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Equal("Project.WorkflowOverrideInvalid", (await ProblemAsync(response)).Code);
        }

        var saved = await SendAsync(admin, HttpMethod.Put, url, new { overrides = new Dictionary<Guid, Guid> { [task] = bug } }, etag);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(bug, (await ReadAsync(saved)).GetProperty("workflowOverrides").GetProperty(task.ToString()).GetGuid());

        var removed = await SendAsync(admin, HttpMethod.Put, url, new { overrides = new Dictionary<Guid, Guid> { [task] = standard } }, saved.Headers.ETag);
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        Assert.Empty((await ReadAsync(removed)).GetProperty("workflowOverrides").EnumerateObject());
    }

    [Fact]
    public async Task Scenario_13_R16_a_type_that_is_the_default_of_a_project_cannot_be_deactivated()
    {
        using var admin = await factory.AdminClientAsync();
        var task = await ConfigApi.TypeByKeyAsync(admin, "task");
        var (_, tag) = await GetAsync(admin, $"/api/v1/work-item-types/{task.GetProperty("id").GetGuid()}");

        var response = await SendAsync(
            admin, HttpMethod.Put, $"/api/v1/work-item-types/{task.GetProperty("id").GetGuid()}", new { name = "مهمة", color = "#1a7a5c", icon = "task", active = false }, tag);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await ProblemAsync(response);
        Assert.Equal("WorkItemType.IsProjectDefault", problem.Code);
        Assert.Contains("ERP", problem.Root.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Scenario_13_R16_a_project_status_with_projects_cannot_be_deactivated_or_removed_from_the_workflow()
    {
        using var admin = await factory.AdminClientAsync();
        var active = await WorkflowApi.StatusByKeyAsync(admin, "project", "active");
        var activeId = active.GetProperty("id").GetGuid();
        var (_, statusTag) = await GetAsync(admin, $"/api/v1/statuses/{activeId}");

        var deactivate = await SendAsync(
            admin, HttpMethod.Put, $"/api/v1/statuses/{activeId}", new { name = "نشط", color = "#1a7a5c", category = "active", active = false }, statusTag);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, deactivate.StatusCode);
        Assert.Equal("Status.InUse", (await ProblemAsync(deactivate)).Code);

        var workflow = await WorkflowApi.WorkflowByKeyAsync(admin, "project_default");
        var kept = workflow.GetProperty("statusIds").EnumerateArray().Select(s => s.GetGuid()).Where(s => s != activeId).ToArray();
        var transitions = workflow.GetProperty("transitions").EnumerateArray()
            .Where(t => t.GetProperty("fromStatusId").GetGuid() != activeId && t.GetProperty("toStatusId").GetGuid() != activeId)
            .Select(t => (object)t.Clone())
            .ToArray();
        var (_, workflowTag) = await GetAsync(admin, $"/api/v1/workflows/{workflow.GetProperty("id").GetGuid()}");

        var remove = await SendAsync(
            admin, HttpMethod.Put, $"/api/v1/workflows/{workflow.GetProperty("id").GetGuid()}",
            new { name = workflow.GetProperty("name").GetString(), statusIds = kept, initialStatusId = workflow.GetProperty("initialStatusId").GetGuid(), transitions }, workflowTag);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, remove.StatusCode);
        Assert.Equal("Workflow.StatusInUse", (await ProblemAsync(remove)).Code);
    }

    [Fact]
    public async Task Scenario_13_R16_a_project_role_given_to_a_member_cannot_be_deleted()
    {
        using var admin = await factory.AdminClientAsync();
        var (roleId, roleTag) = await CreateRoleAsync(admin, Unique("held"), RoleScope.Project, [Permissions.ProjectsView]);
        var owner = await MyIdAsync(admin);
        var manager = await RoleIdAsync(admin, "project_manager");
        var reem = await UserIdAsync(admin, "reem");
        var (id, etag) = await CreateProjectAsync(admin);
        var withMember = await PutMembersAsync(admin, id, MembersBody((owner, manager), (reem, roleId)), etag);
        Assert.Equal(HttpStatusCode.OK, withMember.StatusCode);

        var blocked = await SendAsync(admin, HttpMethod.Delete, $"/api/v1/roles/{roleId}", null, roleTag);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal("Role.InUse", (await ProblemAsync(blocked)).Code);

        await PutMembersAsync(admin, id, MembersBody((owner, manager)), withMember.Headers.ETag);
        var freed = await SendAsync(admin, HttpMethod.Delete, $"/api/v1/roles/{roleId}", null, roleTag);
        Assert.Equal(HttpStatusCode.NoContent, freed.StatusCode);
    }

    private static async Task<Guid> TypeByKeyIdAsync(HttpClient client, string key) => await TypeIdAsync(client, key);
}
