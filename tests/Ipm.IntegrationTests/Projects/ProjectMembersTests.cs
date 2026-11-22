using System.Net;
using Ipm.Domain.Identity;
using Ipm.IntegrationTests.Infrastructure;
using static Ipm.IntegrationTests.Identity.IdentityApi;
using static Ipm.IntegrationTests.Projects.ProjectApi;

namespace Ipm.IntegrationTests.Projects;

/// <summary>Scenarios 7, 8, 14 and 16 (members) of docs/modules/04-projects.md.</summary>
[Collection(ApiCollection.Name)]
public sealed class ProjectMembersTests(ApiFactory factory)
{
    [Fact]
    public async Task Members_are_replaced_as_a_whole_list_and_the_project_etag_moves()
    {
        using var admin = await factory.AdminClientAsync();
        var owner = await MyIdAsync(admin);
        var reem = await UserIdAsync(admin, "reem");
        var manager = await RoleIdAsync(admin, "project_manager");
        var member = await RoleIdAsync(admin, "team_member");
        var viewer = await RoleIdAsync(admin, "viewer");
        var (id, etag) = await CreateProjectAsync(admin);

        var added = await PutMembersAsync(admin, id, MembersBody((owner, manager), (reem, member)), etag);
        Assert.Equal(HttpStatusCode.OK, added.StatusCode);
        Assert.NotEqual(etag, added.Headers.ETag);
        var response = await ReadAsync(added);
        Assert.Equal(2, ProjectOf(response).GetProperty("members").GetArrayLength());
        Assert.Empty(response.GetProperty("warnings").EnumerateArray());

        var reRoled = await PutMembersAsync(admin, id, MembersBody((owner, manager), (reem, viewer)), added.Headers.ETag);
        Assert.Equal(HttpStatusCode.OK, reRoled.StatusCode);
        var roles = ProjectOf(await ReadAsync(reRoled)).GetProperty("members").EnumerateArray().ToDictionary(m => m.GetProperty("userId").GetGuid(), m => m.GetProperty("roleId").GetGuid());
        Assert.Equal(viewer, roles[reem]);

        var removed = await PutMembersAsync(admin, id, MembersBody((owner, manager)), reRoled.Headers.ETag);
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        Assert.Equal(1, ProjectOf(await ReadAsync(removed)).GetProperty("members").GetArrayLength());
    }

    [Fact]
    public async Task Scenario_7_R7_the_owner_stays_a_member_and_every_new_member_is_an_active_user_with_a_project_role()
    {
        using var admin = await factory.AdminClientAsync();
        var owner = await MyIdAsync(admin);
        var reem = await UserIdAsync(admin, "reem");
        var mona = await UserIdAsync(admin, "mona");
        var manager = await RoleIdAsync(admin, "project_manager");
        var member = await RoleIdAsync(admin, "team_member");
        var systemRole = await RoleIdAsync(admin, "staff");
        var (id, etag) = await CreateProjectAsync(admin);

        var withoutOwner = await PutMembersAsync(admin, id, MembersBody((reem, member)), etag);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, withoutOwner.StatusCode);
        Assert.Equal("Project.OwnerMustBeMember", (await ProblemAsync(withoutOwner)).Code);

        var inactive = await PutMembersAsync(admin, id, MembersBody((owner, manager), (mona, member)), etag);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, inactive.StatusCode);
        Assert.Equal("Project.MembersInvalid", (await ProblemAsync(inactive)).Code);

        var systemScoped = await PutMembersAsync(admin, id, MembersBody((owner, manager), (reem, systemRole)), etag);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, systemScoped.StatusCode);
        Assert.Equal("Project.MembersInvalid", (await ProblemAsync(systemScoped)).Code);

        var duplicate = await PutMembersAsync(admin, id, MembersBody((owner, manager), (reem, member), (reem, manager)), etag);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, duplicate.StatusCode);
        Assert.Equal("Project.MembersInvalid", (await ProblemAsync(duplicate)).Code);

        var unknown = await PutMembersAsync(admin, id, MembersBody((owner, manager), (Guid.NewGuid(), member)), etag);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unknown.StatusCode);
        Assert.Equal("Project.MembersInvalid", (await ProblemAsync(unknown)).Code);
    }

    [Fact]
    public async Task Scenario_7_the_owner_can_only_move_to_a_member()
    {
        using var admin = await factory.AdminClientAsync();
        var owner = await MyIdAsync(admin);
        var reem = await UserIdAsync(admin, "reem");
        var fahad = await UserIdAsync(admin, "fahad");
        var manager = await RoleIdAsync(admin, "project_manager");
        var member = await RoleIdAsync(admin, "team_member");
        var (id, etag) = await CreateProjectAsync(admin);

        var notMember = await PutProjectAsync(admin, id, UpdateBody(ownerId: reem), etag);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, notMember.StatusCode);
        Assert.Equal("Project.OwnerMustBeMember", (await ProblemAsync(notMember)).Code);

        var added = await PutMembersAsync(admin, id, MembersBody((owner, manager), (reem, member), (fahad, member)), etag);
        var moved = await PutProjectAsync(admin, id, UpdateBody(ownerId: reem), added.Headers.ETag);
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal(reem, (await ReadAsync(moved)).GetProperty("ownerId").GetGuid());
    }

    [Fact]
    public async Task Scenario_8_R8_a_manager_cannot_hand_out_a_role_with_permissions_he_lacks_in_the_project()
    {
        using var admin = await factory.AdminClientAsync();
        using var khaled = await factory.SignedInClientAsync("khaled", ApiFactory.DemoPassword);
        var strong = await CreateRoleAsync(admin, Unique("strong"), RoleScope.Project, [Permissions.ProjectsView, Permissions.GitManage]);
        var khaledId = await MyIdAsync(khaled);
        var fahad = await UserIdAsync(admin, "fahad");
        var manager = await RoleIdAsync(admin, "project_manager");
        var (id, etag) = await CreateProjectAsync(khaled);

        var refused = await PutMembersAsync(khaled, id, MembersBody((khaledId, manager), (fahad, strong.Id)), etag);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal("Role.PermissionNotGrantable", (await ProblemAsync(refused)).Code);

        var (_, adminTag) = await GetProjectAsync(admin, id);
        var allowed = await PutMembersAsync(admin, id, MembersBody((khaledId, manager), (fahad, strong.Id)), adminTag);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);

        // The same ceiling applies to taking the role away from the member who has it.
        var takeAway = await PutMembersAsync(khaled, id, MembersBody((khaledId, manager)), allowed.Headers.ETag);
        Assert.Equal(HttpStatusCode.Forbidden, takeAway.StatusCode);
    }

    [Fact]
    public async Task Scenario_8_managing_members_needs_the_permission_in_the_project()
    {
        using var fahad = await factory.SignedInClientAsync("fahad", ApiFactory.DemoPassword);
        using var admin = await factory.AdminClientAsync();
        var erp = (await ProjectByCodeAsync(admin, "ERP")).GetProperty("id").GetGuid();
        var (_, tag) = await GetProjectAsync(fahad, erp);

        var response = await PutMembersAsync(fahad, erp, MembersBody(), tag);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Scenario_14_a_role_change_is_felt_on_the_very_next_request()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var editor = await CreateRoleAsync(admin, Unique("editor"), RoleScope.Project, [Permissions.ProjectsView, Permissions.ProjectsEdit]);
        var owner = await MyIdAsync(admin);
        var reemId = await UserIdAsync(admin, "reem");
        var manager = await RoleIdAsync(admin, "project_manager");
        var (id, etag) = await CreateProjectAsync(admin);
        await PutMembersAsync(admin, id, MembersBody((owner, manager), (reemId, editor.Id)), etag);

        var (_, reemTag) = await GetProjectAsync(reem, id);
        Assert.Equal(HttpStatusCode.OK, (await PutProjectAsync(reem, id, UpdateBody("بواسطة ريم"), reemTag)).StatusCode);

        var (role, roleTag) = await GetAsync(admin, $"/api/v1/roles/{editor.Id}");
        var edit = await SendAsync(
            admin, HttpMethod.Put, $"/api/v1/roles/{editor.Id}",
            new { name = role.GetProperty("name").GetString(), description = "x", permissions = new[] { Permissions.ProjectsView }, denied = Array.Empty<string>() }, roleTag);
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);

        var (_, nextTag) = await GetProjectAsync(reem, id);
        Assert.Equal(HttpStatusCode.Forbidden, (await PutProjectAsync(reem, id, UpdateBody("مرة ثانية"), nextTag)).StatusCode);

        // Taking the project membership away makes the project invisible at once.
        var (_, adminTag) = await GetProjectAsync(admin, id);
        await PutMembersAsync(admin, id, MembersBody((owner, manager)), adminTag);
        Assert.Equal(HttpStatusCode.NotFound, (await reem.GetAsync(new Uri($"/api/v1/projects/{id}", UriKind.Relative))).StatusCode);
    }
}
