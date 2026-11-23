using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ipm.IntegrationTests.Config;
using Ipm.IntegrationTests.Git;
using Ipm.IntegrationTests.Infrastructure;
using static Ipm.IntegrationTests.Approvals.ApprovalApi;
using static Ipm.IntegrationTests.Identity.IdentityApi;
using static Ipm.IntegrationTests.Projects.ProjectApi;
using static Ipm.IntegrationTests.Work.WorkApi;

namespace Ipm.IntegrationTests.Dashboard;

/// <summary>Scenarios 9 to 12 of docs/modules/09-dashboard-search-settings.md: the dashboard indicators, their filters and who sees what.</summary>
[Collection(ApiCollection.Name)]
public sealed class DashboardTests(ApiFactory factory)
{
    private static string Day(int offset) => DateTime.UtcNow.AddHours(3).AddDays(offset).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private static async Task<JsonElement> StatsAsync(HttpClient client, string query) => (await GetAsync(client, $"/api/v1/dashboard/stats?{query}")).Body;

    /// <summary>A project with three tasks: one overdue, one not started, one in progress (assigned to reem), and a "blocks" link from the first to the second.</summary>
    private static async Task<(Guid Project, Guid Overdue, Guid Waiting, Guid Moving)> SeedAsync(HttpClient admin)
    {
        var (project, _) = await NewProjectAsync(admin);
        var reem = await UserIdAsync(admin, "reem");
        var (overdue, _, _) = await CreateAsync(admin, project, "task", dueDate: Day(-2));
        var (waiting, _, _) = await CreateAsync(admin, project, "task", dueDate: Day(5));
        var (moving, _, _) = await CreateAsync(admin, project, "task", assignees: [reem]);
        await MoveAsync(admin, moving, "in_progress");
        var blocks = (await ConfigApi.RelationTypeByKeyAsync(admin, "blocks")).GetProperty("id").GetGuid();
        var link = await admin.PostAsJsonAsync(new Uri("/api/v1/relations", UriKind.Relative), new { sourceId = overdue, targetId = waiting, relationTypeId = blocks });
        Assert.Equal(HttpStatusCode.Created, link.StatusCode);
        return (project, overdue, waiting, moving);
    }

    [Fact]
    public async Task Scenario_9_R10_the_indicators_count_by_status_category_type_and_dependency()
    {
        using var admin = await factory.AdminClientAsync();
        var (project, _, _, _) = await SeedAsync(admin);

        var stats = await StatsAsync(admin, $"projectId={project}");
        Assert.Equal(1, stats.GetProperty("totalProjects").GetInt32());
        Assert.Equal(3, stats.GetProperty("openWorkItems").GetInt32());
        Assert.Equal(1, stats.GetProperty("overdueWorkItems").GetInt32());
        Assert.Equal(2, stats.GetProperty("byCategory").GetProperty("not-started").GetInt32());
        Assert.Equal(1, stats.GetProperty("byCategory").GetProperty("active").GetInt32());
        Assert.Equal(0, stats.GetProperty("byCategory").GetProperty("done").GetInt32());
        Assert.Equal(stats.GetProperty("byCategory").GetRawText(), stats.GetProperty("leafByCategory").GetRawText());

        var byType = stats.GetProperty("byType");
        Assert.Equal(1, byType.GetArrayLength());
        Assert.Equal(3, byType[0].GetProperty("total").GetInt32());
        Assert.Equal(await TypeIdAsync(admin, "task"), byType[0].GetProperty("typeId").GetGuid());

        // the overdue item blocks the waiting one: 1 blocked, 1 blocking, 1 of 3 open items = 33%
        Assert.Equal(1, stats.GetProperty("blockedItems").GetInt32());
        var dependencies = stats.GetProperty("dependencies");
        Assert.Equal(1, dependencies.GetProperty("blockedItems").GetInt32());
        Assert.Equal(1, dependencies.GetProperty("blockingItems").GetInt32());
        Assert.Equal(33, dependencies.GetProperty("itemsWithOpenDependencyPct").GetInt32());
    }

    [Fact]
    public async Task Scenario_10_R9_the_period_and_the_filters_narrow_the_items()
    {
        using var admin = await factory.AdminClientAsync();
        var (project, _, _, _) = await SeedAsync(admin);
        var reem = await UserIdAsync(admin, "reem");

        Assert.Equal(1, (await StatsAsync(admin, $"projectId={project}&statusCategory=active")).GetProperty("openWorkItems").GetInt32());
        Assert.Equal(2, (await StatsAsync(admin, $"projectId={project}&statusCategory=not-started,done")).GetProperty("openWorkItems").GetInt32());
        Assert.Equal(1, (await StatsAsync(admin, $"projectId={project}&assigneeId={reem}")).GetProperty("openWorkItems").GetInt32());

        // the overdue item spans [created today, due 2 days ago]: it overlaps a window around now and one that ends before today
        Assert.Equal(0, (await StatsAsync(admin, $"projectId={project}&from=2000-01-01&to=2000-01-31")).GetProperty("openWorkItems").GetInt32());
        Assert.Equal(3, (await StatsAsync(admin, $"projectId={project}&from={Day(-30)}&to={Day(30)}")).GetProperty("openWorkItems").GetInt32());
        Assert.Equal(1, (await StatsAsync(admin, $"projectId={project}&from={Day(5)}&to={Day(6)}")).GetProperty("openWorkItems").GetInt32());

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(new Uri($"/api/v1/dashboard/stats?projectId={project}&statusCategory=bogus", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(new Uri("/api/v1/dashboard/stats?from=2026-02-01&to=2026-01-01", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task Scenario_11_R8_each_caller_gets_the_numbers_of_the_projects_they_see()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        using var noura = await factory.SignedInClientAsync("noura", ApiFactory.DemoPassword);
        var (project, _, _, _) = await SeedAsync(admin);
        var (hidden, _) = await CreateProjectAsync(admin);

        Assert.Equal(3, (await StatsAsync(reem, $"projectId={project}")).GetProperty("openWorkItems").GetInt32());
        // a project the caller cannot see answers with zeros, not with an error that says it exists
        var zeros = await StatsAsync(reem, $"projectId={hidden}");
        Assert.Equal(0, zeros.GetProperty("totalProjects").GetInt32());
        Assert.Equal(0, zeros.GetProperty("openWorkItems").GetInt32());
        Assert.Equal(0, (await StatsAsync(noura, $"projectId={project}")).GetProperty("totalProjects").GetInt32());
    }

    [Fact]
    public async Task Scenario_12_R10_pending_approvals_and_git_activity_are_counted_for_the_caller()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var gated = await GatedItemAsync(admin);
        var before = (await StatsAsync(admin, $"projectId={gated.Project}")).GetProperty("pendingApprovals").GetInt32();

        await SubmitAsync(reem, gated.Template, "work-item", gated.Item);
        Assert.Equal(before + 1, (await StatsAsync(admin, $"projectId={gated.Project}")).GetProperty("pendingApprovals").GetInt32());
        // the requester never waits on their own request
        Assert.Equal(0, (await StatsAsync(reem, $"projectId={gated.Project}")).GetProperty("pendingApprovals").GetInt32());

        var (repo, _) = await GitApi.ConnectAsync(admin, gated.Project);
        Assert.Equal(0, (await StatsAsync(admin, $"projectId={gated.Project}")).GetProperty("gitActivity").GetInt32());
        await GitApi.DeliverAsync(factory, "gitea", repo, GitApi.PushBody("work"));
        Assert.Equal(1, (await StatsAsync(admin, $"projectId={gated.Project}")).GetProperty("gitActivity").GetInt32());
        Assert.Equal(0, (await StatsAsync(admin, $"projectId={gated.Project}&from=2000-01-01&to=2000-01-02")).GetProperty("gitActivity").GetInt32());
    }
}
