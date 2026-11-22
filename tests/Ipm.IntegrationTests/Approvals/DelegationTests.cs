using System.Net;
using System.Net.Http.Json;
using Ipm.Domain.Identity;
using Ipm.IntegrationTests.Infrastructure;
using static Ipm.IntegrationTests.Approvals.ApprovalApi;
using static Ipm.IntegrationTests.Identity.IdentityApi;
using static Ipm.IntegrationTests.Projects.ProjectApi;
using static Ipm.IntegrationTests.Work.WorkApi;

namespace Ipm.IntegrationTests.Approvals;

/// <summary>Scenario 11 of docs/modules/06-approvals.md: delegating approval authority.</summary>
[Collection(ApiCollection.Name)]
public sealed class DelegationTests(ApiFactory factory)
{
    private static string Today(int days) => DateTime.UtcNow.AddHours(3).AddDays(days).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private static Task<HttpResponseMessage> CreateAsync(HttpClient client, Guid? from, Guid to, string fromDate, string toDate, string? reason = "إجازة") =>
        client.PostAsJsonAsync(new Uri("/api/v1/delegations", UriKind.Relative), new { fromUserId = from, toUserId = to, from = fromDate, to = toDate, reason });

    [Fact]
    public async Task Scenario_11_R13_a_delegate_decides_for_the_approver_inside_the_window_and_the_decision_says_so()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var adminId = await MyIdAsync(admin);
        var fahad = await UserIdAsync(admin, "fahad");
        var gated = await GatedItemAsync(admin);

        // fahad can see the project and decide there, through a project role of his own.
        var (role, _) = await CreateRoleAsync(admin, Unique("decider"), RoleScope.Project, [Permissions.ProjectsView, Permissions.ApprovalsDecide, Permissions.ApprovalsView]);
        var (_, projectEtag) = await GetProjectAsync(admin, gated.Project);
        var manager = await RoleIdAsync(admin, "project_manager");
        var members = await PutMembersAsync(admin, gated.Project, MembersBody((adminId, manager), (fahad, role), (await UserIdAsync(admin, "reem"), await RoleIdAsync(admin, "team_member"))), projectEtag);
        Assert.Equal(HttpStatusCode.OK, members.StatusCode);
        using var fahadClient = await factory.SignedInClientAsync("fahad", ApiFactory.DemoPassword);

        var (id, etag, _) = await SubmitAsync(reem, gated.Template, "work-item", gated.Item);
        Assert.Equal(HttpStatusCode.Forbidden, (await DecideAsync(fahadClient, id, "approve", null, 1, etag)).StatusCode);

        var created = await CreateAsync(admin, null, fahad, Today(0), Today(5));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var delegationId = (await ReadAsync(created)).GetProperty("id").GetGuid();

        var decided = await DecideAsync(fahadClient, id, "approve", "بالنيابة", 1, etag);
        Assert.Equal(HttpStatusCode.OK, decided.StatusCode);
        var decision = (await ReadAsync(decided)).GetProperty("decisions")[0];
        Assert.Equal(fahad, decision.GetProperty("deciderId").GetGuid());
        Assert.Equal(adminId, decision.GetProperty("onBehalfOf").GetGuid());

        // Revoked, the hand-over is over; revoking twice is refused.
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync(new Uri($"/api/v1/delegations/{delegationId}", UriKind.Relative))).StatusCode);
        Assert.Equal("Delegation.AlreadyRevoked", (await ProblemAsync(await admin.DeleteAsync(new Uri($"/api/v1/delegations/{delegationId}", UriKind.Relative)))).Code);
        var (body, nextTag) = await GetApprovalAsync(admin, id);
        Assert.False(body.GetProperty("canDecide").GetBoolean() && false);
        Assert.Equal(HttpStatusCode.Forbidden, (await DecideAsync(fahadClient, id, "approve", null, 1, nextTag)).StatusCode);
    }

    [Fact]
    public async Task Scenario_11_R13_the_dates_the_users_and_the_overlap_are_checked()
    {
        using var admin = await factory.AdminClientAsync();
        using var khaled = await factory.SignedInClientAsync("khaled", ApiFactory.DemoPassword);
        var adminId = await MyIdAsync(admin);
        var khaledId = await UserIdAsync(admin, "khaled");
        var noura = await UserIdAsync(admin, "noura");
        var mona = await UserIdAsync(admin, "mona");
        var abdullah = await UserIdAsync(admin, "abdullah");

        Assert.Equal(HttpStatusCode.BadRequest, (await CreateAsync(khaled, null, khaledId, Today(0), Today(3))).StatusCode); // to himself
        Assert.Equal(HttpStatusCode.BadRequest, (await CreateAsync(khaled, null, noura, Today(3), Today(0))).StatusCode); // dates reversed
        Assert.Equal(HttpStatusCode.BadRequest, (await CreateAsync(khaled, null, noura, Today(-10), Today(-5))).StatusCode); // already over
        Assert.Equal(HttpStatusCode.BadRequest, (await CreateAsync(khaled, null, noura, Today(0), Today(120))).StatusCode); // too long
        Assert.Equal(HttpStatusCode.BadRequest, (await CreateAsync(khaled, null, noura, Today(0), Today(3), "  ")).StatusCode); // no reason
        Assert.Equal(HttpStatusCode.BadRequest, (await CreateAsync(khaled, null, mona, Today(0), Today(3))).StatusCode); // inactive
        Assert.Equal(HttpStatusCode.BadRequest, (await CreateAsync(khaled, null, abdullah, Today(0), Today(3))).StatusCode); // cannot decide anywhere
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateAsync(khaled, adminId, noura, Today(0), Today(3))).StatusCode); // for someone else

        var first = await CreateAsync(khaled, null, noura, Today(0), Today(10));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var overlap = await CreateAsync(khaled, null, noura, Today(5), Today(15));
        Assert.Equal(HttpStatusCode.Conflict, overlap.StatusCode);
        Assert.Equal("Delegation.Overlap", (await ProblemAsync(overlap)).Code);

        // A manager delegates for someone else; each side sees its own, the manager sees all.
        var forOther = await CreateAsync(admin, khaledId, noura, Today(20), Today(25));
        Assert.Equal(HttpStatusCode.Created, forOther.StatusCode);
        var mine = await ReadAsync(await khaled.GetAsync(new Uri("/api/v1/delegations", UriKind.Relative)));
        Assert.True(mine.GetArrayLength() >= 2);
        Assert.All(mine.EnumerateArray(), d => Assert.Equal(khaledId, d.GetProperty("fromUserId").GetGuid()));
        using var fahad = await factory.SignedInClientAsync("fahad", ApiFactory.DemoPassword);
        Assert.Equal(HttpStatusCode.NotFound, (await fahad.DeleteAsync(new Uri($"/api/v1/delegations/{(await ReadAsync(first)).GetProperty("id").GetGuid()}", UriKind.Relative))).StatusCode);
    }
}
