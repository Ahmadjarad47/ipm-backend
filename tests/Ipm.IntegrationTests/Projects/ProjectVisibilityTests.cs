using System.Net;
using Ipm.Domain.Identity;
using Ipm.IntegrationTests.Infrastructure;
using static Ipm.IntegrationTests.Identity.IdentityApi;
using static Ipm.IntegrationTests.Projects.ProjectApi;

namespace Ipm.IntegrationTests.Projects;

/// <summary>Scenarios 1–3 of docs/modules/04-projects.md: who sees which project and what a member may do.</summary>
[Collection(ApiCollection.Name)]
public sealed class ProjectVisibilityTests(ApiFactory factory)
{
    private static readonly Uri Projects = new("/api/v1/projects", UriKind.Relative);

    [Fact]
    public async Task Scenario_1_the_administrator_sees_the_four_demo_projects_with_their_members_and_types()
    {
        using var admin = await factory.AdminClientAsync();

        var erp = await ProjectByCodeAsync(admin, "ERP");
        var mail = await ProjectByCodeAsync(admin, "MAIL");

        Assert.Equal(5, erp.GetProperty("members").GetArrayLength());
        Assert.Equal(5, erp.GetProperty("allowedTypeIds").GetArrayLength());
        Assert.False(erp.GetProperty("archived").GetBoolean());
        Assert.True(mail.GetProperty("archived").GetBoolean());
        Assert.Equal(await UserIdAsync(admin, "khaled"), erp.GetProperty("ownerId").GetGuid());
        Assert.Equal(await ProjectStatusIdAsync(admin, "active"), erp.GetProperty("statusId").GetGuid());
        Assert.Equal(await ProjectStatusIdAsync(admin, "pending"), (await ProjectByCodeAsync(admin, "BI")).GetProperty("statusId").GetGuid());
        Assert.Equal(await TypeIdAsync(admin, "task"), erp.GetProperty("defaultTypeId").GetGuid());
        await ProjectByCodeAsync(admin, "SSP");
    }

    [Fact]
    public async Task Scenario_1_a_viewer_sees_only_his_project_and_a_hidden_one_answers_404()
    {
        using var abdullah = await factory.SignedInClientAsync("abdullah", ApiFactory.DemoPassword);
        using var admin = await factory.AdminClientAsync();
        var ssp = await ProjectByCodeAsync(admin, "SSP");

        // Other tests add abdullah to their own fresh projects, so only the demo projects are compared.
        var page = await ReadAsync(await abdullah.GetAsync(new Uri("/api/v1/projects?pageSize=200", UriKind.Relative)));
        var codes = page.GetProperty("items").EnumerateArray().Select(p => p.GetProperty("code").GetString()).ToList();

        Assert.Equal(["ERP"], codes.Where(c => c is "ERP" or "SSP" or "BI" or "MAIL"));

        var hidden = await abdullah.GetAsync(new Uri($"/api/v1/projects/{ssp.GetProperty("id").GetGuid()}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Equal("Project.NotFound", (await ProblemAsync(hidden)).Code);
        Assert.Equal(HttpStatusCode.NotFound, (await abdullah.GetAsync(new Uri($"/api/v1/projects/{Guid.NewGuid()}", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task Scenario_1_a_user_with_no_membership_sees_nothing_and_without_a_token_nothing_is_readable()
    {
        using var admin = await factory.AdminClientAsync();
        var staff = await RoleIdAsync(admin, "staff");
        var user = await ReadyUserAsync(factory, admin, "nobody", staff);
        using var anonymous = factory.CreateClient();

        var page = await ReadAsync(await user.Client.GetAsync(Projects));

        Assert.Equal(0, page.GetProperty("total").GetInt32());
        Assert.Empty(page.GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Projects)).StatusCode);
    }

    [Fact]
    public async Task Scenario_2_me_lists_the_permissions_of_each_project_role()
    {
        using var khaled = await factory.SignedInClientAsync("khaled", ApiFactory.DemoPassword);
        using var fahad = await factory.SignedInClientAsync("fahad", ApiFactory.DemoPassword);
        var erp = (await ProjectByCodeAsync(khaled, "ERP")).GetProperty("id").GetString()!;

        var khaledMe = await ReadAsync(await khaled.GetAsync(new Uri("/api/v1/auth/me", UriKind.Relative)));
        var fahadMe = await ReadAsync(await fahad.GetAsync(new Uri("/api/v1/auth/me", UriKind.Relative)));

        var manager = khaledMe.GetProperty("projectPermissions").GetProperty(erp).EnumerateArray().Select(p => p.GetString()).ToList();
        Assert.Contains(Permissions.ProjectsEdit, manager);
        Assert.Contains(Permissions.ProjectsMembersManage, manager);
        Assert.True(khaledMe.GetProperty("projectPermissions").EnumerateObject().Count() >= 3);

        var member = fahadMe.GetProperty("projectPermissions").GetProperty(erp).EnumerateArray().Select(p => p.GetString()).ToList();
        Assert.Contains(Permissions.TasksCreate, member);
        Assert.DoesNotContain(Permissions.ProjectsEdit, member);
    }

    [Fact]
    public async Task Scenario_3_a_viewer_reads_but_cannot_edit_and_a_non_member_gets_404()
    {
        using var abdullah = await factory.SignedInClientAsync("abdullah", ApiFactory.DemoPassword);
        using var khaled = await factory.SignedInClientAsync("khaled", ApiFactory.DemoPassword);
        using var admin = await factory.AdminClientAsync();
        var erp = (await ProjectByCodeAsync(admin, "ERP")).GetProperty("id").GetGuid();

        var (_, tag) = await GetProjectAsync(abdullah, erp);
        var viewerEdit = await PutProjectAsync(abdullah, erp, UpdateBody("ERP معدّل"), tag);
        Assert.Equal(HttpStatusCode.Forbidden, viewerEdit.StatusCode);
        Assert.Equal("Auth.Forbidden", (await ProblemAsync(viewerEdit)).Code);

        // ERP is active, so its name, dates and owner need an approval (06b); the manager still edits what is not protected.
        var current = (await GetProjectAsync(khaled, erp)).Body;
        var managerEdit = await PutProjectAsync(
            khaled, erp,
            UpdateBody(current.GetProperty("name").GetString()!, start: current.GetProperty("startDate").GetString()!, end: current.GetProperty("endDate").GetString()!),
            tag);
        Assert.Equal(HttpStatusCode.OK, managerEdit.StatusCode);

        var (otherId, _) = await CreateProjectAsync(admin);
        var (_, otherTag) = await GetProjectAsync(admin, otherId);
        var outsider = await PutProjectAsync(khaled, otherId, UpdateBody(), otherTag);
        Assert.Equal(HttpStatusCode.NotFound, outsider.StatusCode);
    }

    [Fact]
    public async Task The_list_filters_by_search_member_archive_state_and_sorts()
    {
        using var admin = await factory.AdminClientAsync();
        var fahad = await UserIdAsync(admin, "fahad");

        var bySearch = await ReadAsync(await admin.GetAsync(new Uri("/api/v1/projects?q=erp", UriKind.Relative)));
        Assert.Contains("ERP", bySearch.GetProperty("items").EnumerateArray().Select(p => p.GetProperty("code").GetString()));

        var archived = await ReadAsync(await admin.GetAsync(new Uri("/api/v1/projects?archived=true&pageSize=200", UriKind.Relative)));
        Assert.Contains("MAIL", archived.GetProperty("items").EnumerateArray().Select(p => p.GetProperty("code").GetString()));
        Assert.All(archived.GetProperty("items").EnumerateArray(), p => Assert.True(p.GetProperty("archived").GetBoolean()));

        var byMember = await ReadAsync(await admin.GetAsync(new Uri($"/api/v1/projects?memberId={fahad}&pageSize=200", UriKind.Relative)));
        var memberCodes = byMember.GetProperty("items").EnumerateArray().Select(p => p.GetProperty("code").GetString()).ToList();
        Assert.Contains("ERP", memberCodes);
        Assert.DoesNotContain("SSP", memberCodes);

        // Other tests add projects of their own, so only the order of the four demo codes is checked.
        string[] demo = ["BI", "ERP", "MAIL", "SSP"];
        var ascending = await ReadAsync(await admin.GetAsync(new Uri("/api/v1/projects?sort=code&pageSize=200", UriKind.Relative)));
        Assert.Equal(demo, ascending.GetProperty("items").EnumerateArray().Select(p => p.GetProperty("code").GetString()!).Where(demo.Contains));

        var descending = await ReadAsync(await admin.GetAsync(new Uri("/api/v1/projects?sort=code&desc=true&pageSize=200", UriKind.Relative)));
        Assert.Equal(demo.Reverse(), descending.GetProperty("items").EnumerateArray().Select(p => p.GetProperty("code").GetString()!).Where(demo.Contains));
    }
}
