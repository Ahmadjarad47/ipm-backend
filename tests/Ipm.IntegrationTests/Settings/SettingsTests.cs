using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Ipm.IntegrationTests.Config;
using Ipm.IntegrationTests.Infrastructure;
using static Ipm.IntegrationTests.Approvals.ApprovalApi;
using static Ipm.IntegrationTests.Identity.IdentityApi;
using static Ipm.IntegrationTests.Projects.ProjectApi;
using static Ipm.IntegrationTests.Work.WorkApi;

namespace Ipm.IntegrationTests.Settings;

/// <summary>Scenarios 1 to 8 of docs/modules/09-dashboard-search-settings.md: the organization settings and what they change in the earlier rules.</summary>
[Collection(ApiCollection.Name)]
public sealed class SettingsTests(ApiFactory factory)
{
    private static readonly object Defaults = new
    {
        sessionHours = 8,
        commentEditHours = 24,
        allowCrossProjectRelations = false,
        allowSelfApproval = false,
        timeZone = "Asia/Riyadh",
        progressIncludedTypeIds = Array.Empty<Guid>(),
        attachmentExtensions = new[] { "pdf", "png", "jpg", "jpeg", "gif", "txt", "csv", "docx", "xlsx", "pptx", "zip" },
    };

    private static Task<(JsonElement Body, EntityTagHeaderValue ETag)> CurrentAsync(HttpClient client) => GetAsync(client, "/api/v1/settings");

    private static Task<HttpResponseMessage> PatchAsync(HttpClient client, object body, EntityTagHeaderValue? etag) =>
        SendAsync(client, HttpMethod.Patch, "/api/v1/settings", body, etag);

    /// <summary>Applies the changes, and always puts the defaults back so a test never leaks a setting into the next.</summary>
    private static async Task WithAsync(HttpClient admin, object changes, Func<Task> body)
    {
        var (_, etag) = await CurrentAsync(admin);
        var changed = await PatchAsync(admin, changes, etag);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        try
        {
            await body();
        }
        finally
        {
            var (_, fresh) = await CurrentAsync(admin);
            Assert.Equal(HttpStatusCode.OK, (await PatchAsync(admin, Defaults, fresh)).StatusCode);
        }
    }

    [Fact]
    public async Task Scenario_1_R1_to_R3_everyone_reads_only_managers_change_and_every_value_is_checked()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);

        var (settings, etag) = await CurrentAsync(reem);
        Assert.Equal(8, settings.GetProperty("sessionHours").GetInt32());
        Assert.Equal("Asia/Riyadh", settings.GetProperty("timeZone").GetString());
        Assert.False(settings.GetProperty("allowCrossProjectRelations").GetBoolean());

        Assert.Equal(HttpStatusCode.Forbidden, (await PatchAsync(reem, new { sessionHours = 9 }, etag)).StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionRequired, (await PatchAsync(admin, new { sessionHours = 9 }, null)).StatusCode);

        var bad = new object[]
        {
            new { unknownKey = 1 }, new { sessionHours = 0 }, new { sessionHours = 25 }, new { commentEditHours = -1 }, new { commentEditHours = 169 },
            new { attachmentMaxMb = 0 }, new { attachmentMaxMb = 101 }, new { auditRetentionDays = 364 }, new { allowSelfApproval = "yes" },
            new { timeZone = "Mars/Olympus" }, new { progressIncludedTypeIds = new[] { Guid.NewGuid() } },
            new { attachmentExtensions = Array.Empty<string>() }, new { attachmentExtensions = new[] { "pdf", "exe" } }, new { attachmentExtensions = new[] { "bad ext" } },
        };
        foreach (var body in bad)
        {
            var response = await PatchAsync(admin, body, etag);
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{JsonSerializer.Serialize(body)} gave {response.StatusCode}");
        }

        var problem = await ReadAsync(await PatchAsync(admin, new { sessionHours = 0 }, etag));
        Assert.True(problem.GetProperty("errors").TryGetProperty("sessionHours", out _));

        await WithAsync(admin, new { sessionHours = 10, attachmentExtensions = new[] { ".PDF", "png", "pdf" } }, async () =>
        {
            var (now, newTag) = await CurrentAsync(reem);
            Assert.Equal(10, now.GetProperty("sessionHours").GetInt32());
            Assert.Equal(["pdf", "png"], now.GetProperty("attachmentExtensions").EnumerateArray().Select(e => e.GetString()));
            Assert.NotEqual(etag.Tag, newTag.Tag);
            Assert.Equal(HttpStatusCode.PreconditionFailed, (await PatchAsync(admin, new { sessionHours = 11 }, etag)).StatusCode);
        });
    }

    [Fact]
    public async Task Scenario_2_R4_a_change_is_audited_for_audit_view_only()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);

        await WithAsync(admin, new { commentEditHours = 12 }, () => Task.CompletedTask);

        var logged = (await GetAsync(admin, "/api/v1/audit?action=settings.updated&pageSize=5")).Body.GetProperty("items");
        Assert.True(logged.GetArrayLength() >= 2);
        Assert.Contains(logged.EnumerateArray(), e => e.GetProperty("changes").EnumerateArray().Any(c => c.GetProperty("field").GetString() == "commentEditHours" && c.GetProperty("to").GetString() == "12"));
        Assert.Equal(0, (await GetAsync(reem, "/api/v1/audit?action=settings.updated")).Body.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Scenario_3_R5_the_comment_edit_window_follows_the_setting()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var (item, _, _) = await CreateAsync(admin, project);
        var created = await reem.PostAsJsonAsync(new Uri("/api/v1/comments", UriKind.Relative), new { subjectType = "work-item", subjectId = item, body = "نص" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await ReadAsync(created)).GetProperty("id").GetGuid();
        var commentTag = created.Headers.ETag;

        await WithAsync(admin, new { commentEditHours = 0 }, async () =>
        {
            await Task.Delay(50);

            var refused = await SendAsync(reem, HttpMethod.Put, $"/api/v1/comments/{id}", new { body = "تعديل" }, commentTag);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        });


        Assert.Equal(HttpStatusCode.OK, (await SendAsync(reem, HttpMethod.Put, $"/api/v1/comments/{id}", new { body = "تعديل" }, commentTag)).StatusCode);
    }

    [Fact]
    public async Task Scenario_4_R5_links_between_projects_follow_the_setting()
    {
        using var admin = await factory.AdminClientAsync();
        var (a, _) = await NewProjectAsync(admin);
        var (b, _) = await NewProjectAsync(admin);
        var (one, _, _) = await CreateAsync(admin, a);
        var (two, _, _) = await CreateAsync(admin, b);
        var kind = (await ConfigApi.RelationTypeByKeyAsync(admin, "relates")).GetProperty("id").GetGuid();
        Task<HttpResponseMessage> Link() => admin.PostAsJsonAsync(new Uri("/api/v1/relations", UriKind.Relative), new { sourceId = one, targetId = two, relationTypeId = kind });

        var refused = await Link();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Equal("Relation.CrossProjectNotAllowed", (await ReadAsync(refused)).GetProperty("code").GetString());

        await WithAsync(admin, new { allowCrossProjectRelations = true }, async () => Assert.Equal(HttpStatusCode.Created, (await Link()).StatusCode));
    }

    [Fact]
    public async Task Scenario_5_R5_self_approval_needs_both_the_setting_and_the_template()
    {
        using var admin = await factory.AdminClientAsync();
        // The requester is a member without the override permission: an administrator's own request is approved at submission (see ApprovalRequestTests).
        using var khaled = await factory.SignedInClientAsync("khaled", ApiFactory.DemoPassword);
        var gated = await GatedItemAsync(admin);
        var khaledId = await MyIdAsync(khaled);
        var (_, projectEtag) = await GetProjectAsync(admin, gated.Project);
        var manager = await RoleIdAsync(admin, "project_manager");
        Assert.Equal(HttpStatusCode.OK, (await PutMembersAsync(admin, gated.Project, MembersBody((await MyIdAsync(admin), manager), (khaledId, manager)), projectEtag)).StatusCode);
        var selfTemplate = await admin.PostAsJsonAsync(
            new Uri("/api/v1/approval-templates", UriKind.Relative),
            new
            {
                name = Unique("ذاتي"), @event = "work-item-approve", stages = new[] { Stage(1, "ذاتي", "user", khaledId.ToString()) }, protectedFields = new[] { "title" }, allowSelfApproval = true,
                rule = new { workItemTypeIds = new[] { gated.TypeId }, actions = new[] { "close" }, priority = 50 },
            });
        Assert.Equal(HttpStatusCode.Created, selfTemplate.StatusCode);
        var selfTemplateId = (await ReadAsync(selfTemplate)).GetProperty("id").GetGuid();

        // the setting is off: nobody decides their own request
        var (own, _, _) = await SubmitAsync(khaled, selfTemplateId, "work-item", gated.Item);
        var (body, etag) = await GetApprovalAsync(khaled, own);
        Assert.Equal(HttpStatusCode.Forbidden, (await DecideAsync(khaled, own, "approve", null, body.GetProperty("revision").GetInt32(), etag)).StatusCode);

        await WithAsync(admin, new { allowSelfApproval = true }, async () =>
        {
            (body, etag) = await GetApprovalAsync(khaled, own);
            Assert.Equal(HttpStatusCode.OK, (await DecideAsync(khaled, own, "approve", null, body.GetProperty("revision").GetInt32(), etag)).StatusCode);

            // the setting is on but this template does not allow it
            var strict = await CreateTemplateAsync(
                admin, "work-item-approve", [Stage(1, "ذاتي", "user", khaledId.ToString())], ["title"], new { workItemTypeIds = new[] { gated.TypeId }, actions = new[] { "close" }, priority = 60 });
            var (other, _, _) = await SubmitAsync(khaled, strict, "work-item", gated.Item);
            var (otherBody, otherTag) = await GetApprovalAsync(khaled, other);
            Assert.Equal(HttpStatusCode.Forbidden, (await DecideAsync(khaled, other, "approve", null, otherBody.GetProperty("revision").GetInt32(), otherTag)).StatusCode);
        });
    }

    [Fact]
    public async Task Scenario_6_R5_only_the_chosen_types_count_toward_progress()
    {
        using var admin = await factory.AdminClientAsync();
        var (project, _) = await NewProjectAsync(admin);
        await CreateAsync(admin, project, "task");
        await CreateAsync(admin, project, "bug");
        var taskType = await TypeIdAsync(admin, "task");

        Assert.Equal(2, (await GetProjectAsync(admin, project)).Body.GetProperty("progress").GetProperty("countedLeaves").GetInt32());

        await WithAsync(admin, new { progressIncludedTypeIds = new[] { taskType } }, async () =>
            Assert.Equal(1, (await GetProjectAsync(admin, project)).Body.GetProperty("progress").GetProperty("countedLeaves").GetInt32()));

        Assert.Equal(2, (await GetProjectAsync(admin, project)).Body.GetProperty("progress").GetProperty("countedLeaves").GetInt32());
    }

    [Fact]
    public async Task Scenario_7_R5_today_follows_the_organization_time_zone()
    {
        using var admin = await factory.AdminClientAsync();
        var (project, _) = await NewProjectAsync(admin);
        // two zones a day apart at any moment: UTC+14 is always ahead of UTC-11
        var west = DateTime.UtcNow.AddHours(-11).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var (item, _, _) = await CreateAsync(admin, project, dueDate: west);

        async Task<bool> OverdueAsync() =>
            (await GetAsync(admin, $"/api/v1/work-items?projectId={project}&overdue=true")).Body.GetProperty("items").EnumerateArray().Any(i => i.GetProperty("id").GetGuid() == item);

        await WithAsync(admin, new { timeZone = "Pacific/Pago_Pago" }, async () => Assert.False(await OverdueAsync()));
        await WithAsync(admin, new { timeZone = "Pacific/Kiritimati" }, async () => Assert.True(await OverdueAsync()));
    }

    [Fact]
    public async Task Scenario_8_R6_a_change_is_read_back_at_once()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);

        await WithAsync(admin, new { commentEditHours = 5 }, async () => Assert.Equal(5, (await CurrentAsync(reem)).Body.GetProperty("commentEditHours").GetInt32()));
        Assert.Equal(24, (await CurrentAsync(reem)).Body.GetProperty("commentEditHours").GetInt32());
    }
}
