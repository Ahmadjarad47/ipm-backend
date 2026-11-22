using System.Net;
using System.Net.Http.Json;
using Ipm.IntegrationTests.Infrastructure;
using static Ipm.IntegrationTests.Identity.IdentityApi;
using static Ipm.IntegrationTests.Projects.ProjectApi;

namespace Ipm.IntegrationTests.Projects;

/// <summary>Scenario 15 of docs/modules/04-projects.md: project templates.</summary>
[Collection(ApiCollection.Name)]
public sealed class ProjectTemplatesTests(ApiFactory factory)
{
    private static readonly Uri Templates = new("/api/v1/project-templates", UriKind.Relative);

    [Fact]
    public async Task The_two_default_templates_are_readable_by_any_signed_in_user()
    {
        using var noura = await factory.SignedInClientAsync("noura", ApiFactory.DemoPassword);
        var task = await TypeIdAsync(noura, "task");
        var bug = await TypeIdAsync(noura, "bug");

        var list = (await ReadAsync(await noura.GetAsync(Templates))).EnumerateArray().ToList();
        var ops = list.Single(t => t.GetProperty("name").GetString() == "مشروع تشغيلي");
        var software = list.Single(t => t.GetProperty("name").GetString() == "مشروع تطوير برمجي");

        Assert.Equal([task, bug], ops.GetProperty("allowedTypeIds").EnumerateArray().Select(t => t.GetGuid()));
        Assert.Equal(45, ops.GetProperty("durationDays").GetInt32());
        Assert.Equal(5, software.GetProperty("allowedTypeIds").GetArrayLength());
        Assert.Equal(120, software.GetProperty("durationDays").GetInt32());
        Assert.Equal(await ProjectStatusIdAsync(noura, "pending"), ops.GetProperty("initialStatusId").GetGuid());
        Assert.NotNull((await noura.GetAsync(new Uri($"/api/v1/project-templates/{ops.GetProperty("id").GetGuid()}", UriKind.Relative))).Headers.ETag);

        var activeOnly = await ReadAsync(await noura.GetAsync(new Uri("/api/v1/project-templates?active=true", UriKind.Relative)));
        Assert.All(activeOnly.EnumerateArray(), t => Assert.True(t.GetProperty("active").GetBoolean()));
    }

    [Fact]
    public async Task Creating_a_template_needs_settings_manage()
    {
        using var noura = await factory.SignedInClientAsync("noura", ApiFactory.DemoPassword);
        using var admin = await factory.AdminClientAsync();
        var task = await TypeIdAsync(admin, "task");
        var body = new { name = Unique("قالب"), allowedTypeIds = new[] { task }, defaultTypeId = task, suggestedRoleIds = Array.Empty<Guid>(), durationDays = 20 };

        Assert.Equal(HttpStatusCode.Forbidden, (await noura.PostAsJsonAsync(Templates, body)).StatusCode);
        var created = await admin.PostAsJsonAsync(Templates, body);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.ETag);
        Assert.Equal(await ProjectStatusIdAsync(admin, "pending"), (await ReadAsync(created)).GetProperty("initialStatusId").GetGuid());
    }

    [Fact]
    public async Task R14_the_types_status_roles_members_and_duration_are_validated()
    {
        using var admin = await factory.AdminClientAsync();
        var task = await TypeIdAsync(admin, "task");
        var bug = await TypeIdAsync(admin, "bug");
        var member = await RoleIdAsync(admin, "team_member");
        var systemRole = await RoleIdAsync(admin, "staff");
        var active = await ProjectStatusIdAsync(admin, "active");

        async Task<(HttpStatusCode Status, string? Code)> Post(object body)
        {
            var response = await admin.PostAsJsonAsync(Templates, body);
            return (response.StatusCode, response.StatusCode == HttpStatusCode.Created ? null : (await ProblemAsync(response)).Code);
        }

        string Name() => Unique("قالب");
        Guid[] none = [];

        Assert.Equal((HttpStatusCode.UnprocessableEntity, "ProjectTemplate.InvalidInitialStatus"),
            await Post(new { name = Name(), allowedTypeIds = new[] { task }, defaultTypeId = task, initialStatusId = active, suggestedRoleIds = none, durationDays = 5 }));
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "ProjectTemplate.InvalidTypes"),
            await Post(new { name = Name(), allowedTypeIds = new[] { task, Guid.NewGuid() }, defaultTypeId = task, suggestedRoleIds = none, durationDays = 5 }));
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "ProjectTemplate.InvalidTypes"),
            await Post(new { name = Name(), allowedTypeIds = new[] { task }, defaultTypeId = bug, suggestedRoleIds = none, durationDays = 5 }));
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "ProjectTemplate.InvalidTypes"),
            await Post(new { name = Name(), allowedTypeIds = none, defaultTypeId = task, suggestedRoleIds = none, durationDays = 5 }));
        Assert.Equal((HttpStatusCode.BadRequest, "ProjectTemplate.InvalidDuration"),
            await Post(new { name = Name(), allowedTypeIds = new[] { task }, defaultTypeId = task, suggestedRoleIds = none, durationDays = 0 }));
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "ProjectTemplate.InvalidRoles"),
            await Post(new { name = Name(), allowedTypeIds = new[] { task }, defaultTypeId = task, suggestedRoleIds = new[] { systemRole }, durationDays = 5 }));
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "ProjectTemplate.InvalidMembers"),
            await Post(new { name = Name(), allowedTypeIds = new[] { task }, defaultTypeId = task, suggestedRoleIds = new[] { member }, durationDays = 5, suggestedMembers = new[] { new { userId = (Guid?)Guid.NewGuid(), roleId = member } } }));
    }

    [Fact]
    public async Task R15_template_names_are_unique_ignoring_case()
    {
        using var admin = await factory.AdminClientAsync();
        var task = await TypeIdAsync(admin, "task");
        var name = Unique("Dup");
        var body = new { name, allowedTypeIds = new[] { task }, defaultTypeId = task, suggestedRoleIds = Array.Empty<Guid>(), durationDays = 5 };
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync(Templates, body)).StatusCode);

        var again = await admin.PostAsJsonAsync(Templates, body with { name = name.ToUpperInvariant() });

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("ProjectTemplate.NameTaken", (await ProblemAsync(again)).Code);
    }

    [Fact]
    public async Task Changing_a_template_never_touches_a_project_made_from_it_and_its_etag_is_checked()
    {
        using var admin = await factory.AdminClientAsync();
        var task = await TypeIdAsync(admin, "task");
        var bug = await TypeIdAsync(admin, "bug");
        var created = await admin.PostAsJsonAsync(
            Templates, new { name = Unique("قالب"), allowedTypeIds = new[] { task }, defaultTypeId = task, suggestedRoleIds = Array.Empty<Guid>(), durationDays = 7 });
        var template = await ReadAsync(created);
        var templateId = template.GetProperty("id").GetGuid();
        var etag = created.Headers.ETag;
        var (projectId, _) = await CreateProjectAsync(admin, templateId: templateId);

        var update = new { name = template.GetProperty("name").GetString(), allowedTypeIds = new[] { task, bug }, defaultTypeId = bug, suggestedRoleIds = Array.Empty<Guid>(), durationDays = 14 };
        var first = await SendAsync(admin, HttpMethod.Put, $"/api/v1/project-templates/{templateId}", update, etag);
        var stale = await SendAsync(admin, HttpMethod.Put, $"/api/v1/project-templates/{templateId}", update, etag);
        var missing = await SendAsync(admin, HttpMethod.Put, $"/api/v1/project-templates/{templateId}", update, null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(14, (await ReadAsync(first)).GetProperty("durationDays").GetInt32());
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionRequired, missing.StatusCode);

        var (project, _) = await GetProjectAsync(admin, projectId);
        Assert.Equal([task], project.GetProperty("allowedTypeIds").EnumerateArray().Select(t => t.GetGuid()));
        Assert.Equal(task, project.GetProperty("defaultTypeId").GetGuid());
    }

    [Fact]
    public async Task A_template_can_be_deactivated_by_an_update_and_stays_readable()
    {
        using var admin = await factory.AdminClientAsync();
        var task = await TypeIdAsync(admin, "task");
        var created = await admin.PostAsJsonAsync(
            Templates, new { name = Unique("قالب"), allowedTypeIds = new[] { task }, defaultTypeId = task, suggestedRoleIds = Array.Empty<Guid>(), durationDays = 7 });
        var template = await ReadAsync(created);
        var id = template.GetProperty("id").GetGuid();

        var off = await SendAsync(
            admin, HttpMethod.Put, $"/api/v1/project-templates/{id}",
            new { name = template.GetProperty("name").GetString(), active = false, allowedTypeIds = new[] { task }, defaultTypeId = task, suggestedRoleIds = Array.Empty<Guid>(), durationDays = 7 },
            created.Headers.ETag);

        Assert.Equal(HttpStatusCode.OK, off.StatusCode);
        Assert.False((await ReadAsync(off)).GetProperty("active").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(new Uri($"/api/v1/project-templates/{id}", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await admin.DeleteAsync(new Uri($"/api/v1/project-templates/{id}", UriKind.Relative))).StatusCode);
    }
}
