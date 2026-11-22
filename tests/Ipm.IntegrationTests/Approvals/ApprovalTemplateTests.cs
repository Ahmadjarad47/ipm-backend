using System.Net;
using System.Net.Http.Json;
using Ipm.IntegrationTests.Infrastructure;
using static Ipm.IntegrationTests.Approvals.ApprovalApi;
using static Ipm.IntegrationTests.Identity.IdentityApi;
using static Ipm.IntegrationTests.Projects.ProjectApi;
using static Ipm.IntegrationTests.Work.WorkApi;

namespace Ipm.IntegrationTests.Approvals;

/// <summary>Scenarios 1 and 2 of docs/modules/06-approvals.md: templates and the match.</summary>
[Collection(ApiCollection.Name)]
public sealed class ApprovalTemplateTests(ApiFactory factory)
{
    private static Task<HttpResponseMessage> PostTemplateAsync(HttpClient client, object body) =>
        client.PostAsJsonAsync(new Uri("/api/v1/approval-templates", UriKind.Relative), body);

    [Fact]
    public async Task Demo_data_the_three_default_templates_exist_and_any_signed_in_user_reads_them()
    {
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);

        var page = await ReadAsync(await reem.GetAsync(new Uri("/api/v1/approval-templates?pageSize=200", UriKind.Relative)));

        var names = page.GetProperty("items").EnumerateArray().Select(t => t.GetProperty("name").GetString()).ToList();
        Assert.Contains("اعتماد مشروع جديد", names);
        Assert.Contains("اعتماد إنجاز مهمة", names);
        Assert.Contains("إغلاق مشروع", names);
    }

    [Fact]
    public async Task Scenario_1_R1_only_a_configurer_writes_and_every_structural_problem_has_its_path()
    {
        using var admin = await factory.AdminClientAsync();
        using var khaled = await factory.SignedInClientAsync("khaled", ApiFactory.DemoPassword);
        var adminId = await MyIdAsync(admin);
        var fahad = await UserIdAsync(admin, "fahad");
        var mona = await UserIdAsync(admin, "mona");

        var good = new { name = Unique("قالب"), @event = "project-close", stages = ManagerStages(adminId), protectedFields = new[] { "endDate" } };
        Assert.Equal(HttpStatusCode.Forbidden, (await PostTemplateAsync(khaled, good)).StatusCode);

        var created = await PostTemplateAsync(admin, good);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await ReadAsync(created)).GetProperty("id").GetGuid();
        Assert.NotNull(created.Headers.ETag);

        async Task<string[]> ErrorsOf(object stages, string[]? fields = null)
        {
            var response = await PostTemplateAsync(admin, new { name = Unique("قالب"), @event = "project-close", stages, protectedFields = fields ?? ["endDate"] });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            return (await ReadAsync(response)).GetProperty("errors").EnumerateObject().Select(e => e.Name).ToArray();
        }

        Assert.Contains("stages", await ErrorsOf(Array.Empty<object>()));
        Assert.Contains("stages", await ErrorsOf(new[] { Stage(1, "أ", "user", adminId.ToString()), Stage(3, "ب", "user", fahad.ToString()) }));
        Assert.Contains("stages[1].approverId", await ErrorsOf(new[] { Stage(1, "أ", "user", adminId.ToString()), Stage(2, "ب", "user", adminId.ToString()) }));
        Assert.Contains("stages[0].approverId", await ErrorsOf(new[] { Stage(1, "أ", "user", mona.ToString()) }));
        Assert.Contains("stages[0].approverId", await ErrorsOf(new[] { Stage(1, "أ", "user", Guid.NewGuid().ToString()) }));
        Assert.Contains("stages[0].approverId", await ErrorsOf(new[] { Stage(1, "أ", "role", Guid.NewGuid().ToString()) }));
        Assert.Contains("stages[0].approverId", await ErrorsOf(new[] { Stage(1, "أ", "position", "boss") }));
        Assert.Contains("stages[0].dueHours", await ErrorsOf(new[] { Stage(1, "أ", "user", adminId.ToString(), 0) }));
        Assert.Contains("protectedFields", await ErrorsOf(new[] { Stage(1, "أ", "user", adminId.ToString()) }, ["title"]));

        var (_, etag) = await GetAsync(admin, $"/api/v1/approval-templates/{id}");
        var updated = await SendAsync(
            admin, HttpMethod.Put, $"/api/v1/approval-templates/{id}",
            new { name = "قالب معدّل", @event = "project-close", active = false, stages = ManagerStages(adminId), protectedFields = new[] { "endDate" } }, etag);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.False((await ReadAsync(updated)).GetProperty("active").GetBoolean());
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await SendAsync(
            admin, HttpMethod.Put, $"/api/v1/approval-templates/{id}", new { name = "x", @event = "project-close", stages = ManagerStages(adminId), protectedFields = new[] { "endDate" } }, etag)).StatusCode);
    }

    [Fact]
    public async Task Scenario_1_R2_the_event_of_a_template_with_requests_is_locked()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var gated = await GatedItemAsync(admin);
        await SubmitAsync(reem, gated.Template, "work-item", gated.Item);

        var (_, etag) = await GetAsync(admin, $"/api/v1/approval-templates/{gated.Template}");
        var changed = await SendAsync(
            admin, HttpMethod.Put, $"/api/v1/approval-templates/{gated.Template}",
            new { name = "x", @event = "project-close", stages = ManagerStages(await MyIdAsync(admin)), protectedFields = new[] { "endDate" } }, etag);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, changed.StatusCode);
        Assert.Equal("ApprovalTemplate.EventLocked", (await ProblemAsync(changed)).Code);
    }

    [Fact]
    public async Task Scenario_2_R3_the_match_prefers_priority_then_specificity_then_age_and_says_null_when_none_governs()
    {
        using var admin = await factory.AdminClientAsync();
        var adminId = await MyIdAsync(admin);
        var (project, _) = await NewProjectAsync(admin);
        var (item, _, body) = await CreateAsync(admin, project, "task", "مهمة المطابقة");
        var type = body.GetProperty("typeId").GetGuid();
        var narrowRule = new { projectIds = new[] { project }, actions = new[] { "close" }, priority = 10 };

        string Url() => $"/api/v1/approval-templates/match?subjectType=work-item&subjectId={item}&action=close";
        var none = await admin.GetAsync(new Uri($"/api/v1/approval-templates/match?subjectType=project&subjectId={project}&action=reopen", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, none.StatusCode);
        Assert.Equal("null", (await none.Content.ReadAsStringAsync()).Trim());

        var wide = await CreateTemplateAsync(admin, "work-item-approve", ManagerStages(adminId), ["title"], new { projectIds = new[] { project }, priority = 10 });
        var matched = await ReadAsync(await admin.GetAsync(new Uri(Url(), UriKind.Relative)));
        Assert.Equal(wide, matched.GetProperty("id").GetGuid());

        var specific = await CreateTemplateAsync(admin, "work-item-approve", ManagerStages(adminId), ["title"], narrowRule);
        Assert.Equal(specific, (await ReadAsync(await admin.GetAsync(new Uri(Url(), UriKind.Relative)))).GetProperty("id").GetGuid());

        var weighted = await CreateTemplateAsync(
            admin, "work-item-approve", ManagerStages(adminId), ["title"], new { projectIds = new[] { project }, workItemTypeIds = new[] { type }, actions = new[] { "close" }, priority = -1 });
        Assert.Equal(specific, (await ReadAsync(await admin.GetAsync(new Uri(Url(), UriKind.Relative)))).GetProperty("id").GetGuid());
        var higher = await CreateTemplateAsync(admin, "work-item-approve", ManagerStages(adminId), ["title"], new { projectIds = new[] { project }, priority = 20 });
        Assert.Equal(higher, (await ReadAsync(await admin.GetAsync(new Uri(Url(), UriKind.Relative)))).GetProperty("id").GetGuid());
        Assert.NotEqual(weighted, higher);

        // A different action or an invalid one.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(new Uri($"/api/v1/approval-templates/match?subjectType=work-item&subjectId={item}&action=fly", UriKind.Relative))).StatusCode);
    }
}
