using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Ipm.Infrastructure.Persistence;
using Ipm.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Ipm.IntegrationTests.Identity.IdentityApi;
using static Ipm.IntegrationTests.Projects.ProjectApi;
using static Ipm.IntegrationTests.Work.WorkApi;

namespace Ipm.IntegrationTests.Activity;

/// <summary>Scenarios 1 to 5 and 6 of docs/modules/08-activity-audit-notifications.md: reading, filtering, the append-only chain, the export and the activity feeds.</summary>
[Collection(ApiCollection.Name)]
public sealed class AuditTests(ApiFactory factory)
{
    private static async Task<JsonElement> AuditAsync(HttpClient client, string query) => (await GetAsync(client, $"/api/v1/audit?{query}")).Body.GetProperty("items");

    private static string[] Actions(JsonElement items) => [.. items.EnumerateArray().Select(e => e.GetProperty("action").GetString()!)];

    [Fact]
    public async Task Scenario_1_R1_to_R3_events_are_readable_and_each_caller_sees_only_what_the_rules_allow()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        using var noura = await factory.SignedInClientAsync("noura", ApiFactory.DemoPassword);
        var (a, _) = await NewProjectAsync(admin);
        var (b, _) = await CreateProjectAsync(admin);
        var (itemId, etag, item) = await CreateAsync(admin, a);

        var response = await PutItemAsync(admin, itemId, UpdateBody(item, title: "عنوان جديد"), etag);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var mine = await AuditAsync(admin, $"projectId={a}");
        var actions = Actions(mine);
        Assert.Contains("project.created", actions);
        Assert.Contains("workItem.created", actions);
        Assert.Contains("workItem.updated", actions);
        Assert.All(mine.EnumerateArray(), e => Assert.Equal(a, e.GetProperty("projectId").GetGuid()));

        var updated = mine.EnumerateArray().First(e => e.GetProperty("action").GetString() == "workItem.updated");
        Assert.False(string.IsNullOrEmpty(updated.GetProperty("actorName").GetString()));
        Assert.Equal("api", updated.GetProperty("source").GetString());
        Assert.Contains(updated.GetProperty("changes").EnumerateArray(), c => c.GetProperty("field").GetString() == "title");
        Assert.False(string.IsNullOrEmpty(updated.GetProperty("summary").GetString()));

        // a member of project A sees A and never B, security events or events without a project
        Assert.True((await AuditAsync(reem, $"projectId={a}")).GetArrayLength() > 0);
        Assert.Equal(0, (await AuditAsync(reem, $"projectId={b}")).GetArrayLength());
        Assert.Equal(0, (await AuditAsync(reem, "security=true")).GetArrayLength());
        Assert.All((await AuditAsync(reem, "pageSize=200")).EnumerateArray(), e =>
        {
            Assert.False(e.GetProperty("security").GetBoolean());
            Assert.NotEqual(JsonValueKind.Null, e.GetProperty("projectId").ValueKind);
        });

        // someone outside the project sees nothing of it, and the holder of audit.view sees the sign-ins too
        Assert.Equal(0, (await AuditAsync(noura, $"projectId={a}")).GetArrayLength());
        var security = await AuditAsync(admin, "security=true");
        Assert.True(security.GetArrayLength() > 0);
        Assert.All(security.EnumerateArray(), e => Assert.True(e.GetProperty("security").GetBoolean()));
    }

    [Fact]
    public async Task Scenario_2_R4_the_filters_work_and_an_unknown_source_is_a_400()
    {
        using var admin = await factory.AdminClientAsync();
        var (project, _) = await NewProjectAsync(admin);
        var (itemId, etag, item) = await CreateAsync(admin, project);
        var reference = item.GetProperty("ref").GetString()!;
        var correlation = $"corr-{Guid.NewGuid():N}";

        admin.DefaultRequestHeaders.Add("X-Correlation-Id", correlation);
        admin.DefaultRequestHeaders.Add("X-Client", "ui");
        Assert.Equal(HttpStatusCode.OK, (await PutItemAsync(admin, itemId, UpdateBody(item, title: "بعنوان آخر"), etag)).StatusCode);
        admin.DefaultRequestHeaders.Remove("X-Correlation-Id");
        admin.DefaultRequestHeaders.Remove("X-Client");

        var byCorrelation = await AuditAsync(admin, $"correlationId={correlation}");
        Assert.True(byCorrelation.GetArrayLength() >= 1);
        Assert.All(byCorrelation.EnumerateArray(), e =>
        {
            Assert.Equal(correlation, e.GetProperty("correlationId").GetString());
            Assert.Equal("ui", e.GetProperty("source").GetString());
        });

        var today = DateTime.UtcNow.AddHours(3).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        Assert.True((await AuditAsync(admin, $"subjectId={itemId}&from={today}&to={today}")).GetArrayLength() >= 1);
        Assert.Equal(0, (await AuditAsync(admin, $"subjectId={itemId}&from=2000-01-01&to=2000-01-02")).GetArrayLength());
        Assert.True((await AuditAsync(admin, $"search={reference}&projectId={project}")).GetArrayLength() >= 1);
        Assert.Equal(["workItem.updated"], Actions(await AuditAsync(admin, $"subjectId={itemId}&action=workItem.updated")).Distinct());
        Assert.Equal(["workItem.created"], Actions(await AuditAsync(admin, $"subjectId={itemId}&action=workItem.created")).Distinct());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(new Uri("/api/v1/audit?source=bogus", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(new Uri("/api/v1/audit?from=2026-02-01&to=2026-01-01", UriKind.Relative))).StatusCode);
        Assert.True((await GetAsync(admin, "/api/v1/audit?pageSize=500")).Body.GetProperty("pageSize").GetInt32() <= 200);
    }

    [Fact]
    public async Task Scenario_3_R5_the_log_refuses_update_delete_and_truncate()
    {
        using var admin = await factory.AdminClientAsync();
        var (project, _) = await NewProjectAsync(admin);
        await CreateAsync(admin, project);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("UPDATE public.audit_log SET source = 'x'"));
        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM public.audit_log"));
        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("TRUNCATE public.audit_log"));
    }

    [Fact]
    public async Task Scenario_4_R6_R7_the_chain_is_sealed_under_parallel_writes_and_a_tampered_row_is_found()
    {
        using var admin = await factory.AdminClientAsync();
        var (project, _) = await NewProjectAsync(admin);

        await Task.WhenAll(Enumerable.Range(0, 30).Select(_ => CreateAsync(admin, project)));

        var verified = (await GetAsync(admin, "/api/v1/audit/verify")).Body;
        Assert.True(verified.GetProperty("ok").GetBoolean());
        Assert.True(verified.GetProperty("checked").GetInt64() >= 30);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var row = (await db.Database.SqlQuery<Guid>($"SELECT id AS \"Value\" FROM public.audit_log WHERE subject_label IS NOT NULL ORDER BY seq DESC LIMIT 1").ToListAsync()).Single();
        var label = (await db.Database.SqlQuery<string>($"SELECT subject_label AS \"Value\" FROM public.audit_log WHERE id = {row}").ToListAsync()).Single();

        try
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE public.audit_log DISABLE TRIGGER audit_log_no_change");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE public.audit_log SET subject_label = 'tampered' WHERE id = {row}");
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE public.audit_log ENABLE TRIGGER audit_log_no_change");

            var broken = (await GetAsync(admin, "/api/v1/audit/verify")).Body;
            Assert.False(broken.GetProperty("ok").GetBoolean());
            Assert.Equal(row, broken.GetProperty("brokenAt").GetGuid());
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE public.audit_log DISABLE TRIGGER audit_log_no_change");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE public.audit_log SET subject_label = {label} WHERE id = {row}");
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE public.audit_log ENABLE TRIGGER audit_log_no_change");
        }

        Assert.True((await GetAsync(admin, "/api/v1/audit/verify")).Body.GetProperty("ok").GetBoolean());

        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        Assert.Equal(HttpStatusCode.Forbidden, (await reem.GetAsync(new Uri("/api/v1/audit/verify", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task Scenario_5_R8_the_export_needs_audit_export_neutralises_formulas_and_is_itself_logged()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var (_, etag) = await GetProjectAsync(admin, project);
        var rename = await PutProjectAsync(admin, project, UpdateBody(name: "=HYPERLINK(1)", ownerId: await MyIdAsync(admin)), etag);
        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await reem.GetAsync(new Uri($"/api/v1/audit/export?projectId={project}", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(new Uri("/api/v1/audit/export?format=xlsx", UriKind.Relative))).StatusCode);

        var response = await admin.GetAsync(new Uri($"/api/v1/audit/export?projectId={project}&subjectId={project}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("text/csv", response.Content.Headers.ContentType!.ToString(), StringComparison.Ordinal);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        var text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        Assert.StartsWith("seq,at,actorId", text, StringComparison.Ordinal);
        Assert.Contains("\"'=HYPERLINK(1)\"", text, StringComparison.Ordinal);
        Assert.Contains("project.created", text, StringComparison.Ordinal);

        var logged = await AuditAsync(admin, "action=audit.exported&pageSize=5");
        Assert.True(logged.GetArrayLength() >= 1);
        Assert.Equal("audit.exported", logged[0].GetProperty("action").GetString());
    }

    [Fact]
    public async Task Scenario_6_R9_the_feeds_of_an_item_and_a_project_follow_the_visibility_rules()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var (other, _) = await CreateProjectAsync(admin);
        var (itemId, etag, item) = await CreateAsync(admin, project);
        Assert.Equal(HttpStatusCode.OK, (await PutItemAsync(admin, itemId, UpdateBody(item, title: "تعديل"), etag)).StatusCode);
        var comment = await admin.PostAsJsonAsync(new Uri("/api/v1/comments", UriKind.Relative), new { subjectType = "work-item", subjectId = itemId, body = "تعليق للنشاط" });
        Assert.Equal(HttpStatusCode.Created, comment.StatusCode);

        var feed = (await GetAsync(reem, $"/api/v1/work-items/{itemId}/activity")).Body.GetProperty("items");
        var actions = Actions(feed);
        Assert.Contains("workItem.created", actions);
        Assert.Contains("workItem.updated", actions);
        Assert.Contains("comment.created", actions);
        Assert.All(feed.EnumerateArray(), e => Assert.False(e.GetProperty("security").GetBoolean()));
        var at = feed.EnumerateArray().Select(e => e.GetProperty("at").GetDateTimeOffset()).ToList();
        Assert.Equal(at.OrderByDescending(x => x), at);

        var projectFeed = (await GetAsync(reem, $"/api/v1/projects/{project}/activity")).Body.GetProperty("items");
        Assert.Contains("workItem.created", Actions(projectFeed));

        Assert.Equal(HttpStatusCode.NotFound, (await reem.GetAsync(new Uri($"/api/v1/projects/{other}/activity", UriKind.Relative))).StatusCode);
    }
}
