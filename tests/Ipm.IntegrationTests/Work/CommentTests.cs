using System.Net;
using System.Net.Http.Json;
using Ipm.Infrastructure.Persistence;
using Ipm.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Ipm.IntegrationTests.Identity.IdentityApi;
using static Ipm.IntegrationTests.Projects.ProjectApi;
using static Ipm.IntegrationTests.Work.WorkApi;

namespace Ipm.IntegrationTests.Work;

/// <summary>Scenarios 12 to 15 of docs/modules/05b-relations-comments.md: comments on projects and work items.</summary>
[Collection(ApiCollection.Name)]
public sealed class CommentTests(ApiFactory factory)
{
    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string subjectType, Guid subjectId, string body) =>
        client.PostAsJsonAsync(new Uri("/api/v1/comments", UriKind.Relative), new { subjectType, subjectId, body });

    private static async Task<JsonElementAndTag> AddAsync(HttpClient client, string subjectType, Guid subjectId, string body)
    {
        var response = await PostAsync(client, subjectType, subjectId, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return new JsonElementAndTag(await ReadAsync(response), response.Headers.ETag!);
    }

    private sealed record JsonElementAndTag(System.Text.Json.JsonElement Body, System.Net.Http.Headers.EntityTagHeaderValue ETag);

    [Fact]
    public async Task Scenario_12_R13_a_member_comments_and_the_mentions_name_only_active_members()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        using var abdullah = await factory.SignedInClientAsync("abdullah", ApiFactory.DemoPassword);
        using var noura = await factory.SignedInClientAsync("noura", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var (item, _, _) = await CreateAsync(admin, project);
        var fahad = await UserIdAsync(admin, "fahad");

        var comment = await AddAsync(reem, "work-item", item, "  شكراً @fahad وكذلك @noura و@mona  ");
        Assert.Equal("شكراً @fahad وكذلك @noura و@mona", comment.Body.GetProperty("body").GetString());
        Assert.Equal([fahad], comment.Body.GetProperty("mentionedUserIds").EnumerateArray().Select(m => m.GetGuid()).ToList());
        Assert.Equal("work-item", comment.Body.GetProperty("subjectType").GetString());
        Assert.False(comment.Body.GetProperty("edited").GetBoolean());

        await AddAsync(admin, "project", project, "تعليق على المشروع");
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(reem, "work-item", item, "   ")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(reem, "work-item", item, new string('س', 5001))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(abdullah, "work-item", item, "لا أملك الصلاحية")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PostAsync(noura, "work-item", item, "خارج المشروع")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PostAsync(admin, "work-item", Guid.NewGuid(), "غير موجود")).StatusCode);

        var listed = await ReadAsync(await abdullah.GetAsync(new Uri($"/api/v1/comments?subjectType=work-item&subjectId={item}", UriKind.Relative)));
        Assert.Equal(1, listed.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await noura.GetAsync(new Uri($"/api/v1/comments?subjectType=work-item&subjectId={item}", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(new Uri($"/api/v1/comments?subjectType=approval&subjectId={item}", UriKind.Relative))).StatusCode);

        var (_, projectEtag) = await GetProjectAsync(admin, project);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(admin, HttpMethod.Post, $"/api/v1/projects/{project}/archive", null, projectEtag)).StatusCode);
        var archived = await PostAsync(admin, "project", project, "بعد الأرشفة");
        Assert.Equal("Comment.ProjectArchived", (await ProblemAsync(archived)).Code);
    }

    [Fact]
    public async Task Scenario_13_R14_only_the_author_edits_inside_the_window_and_every_edit_adds_a_revision()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        using var fahad = await factory.SignedInClientAsync("fahad", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var (item, _, _) = await CreateAsync(admin, project);
        var comment = await AddAsync(reem, "work-item", item, "النص الأول");
        var id = comment.Body.GetProperty("id").GetGuid();

        var edited = await SendAsync(reem, HttpMethod.Put, $"/api/v1/comments/{id}", new { body = "النص المعدّل" }, comment.ETag);
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.NotEqual(comment.ETag, edited.Headers.ETag);
        var after = await ReadAsync(edited);
        Assert.True(after.GetProperty("edited").GetBoolean());
        Assert.Equal("النص المعدّل", after.GetProperty("body").GetString());

        var revisions = await ReadAsync(await fahad.GetAsync(new Uri($"/api/v1/comments/{id}/revisions", UriKind.Relative)));
        Assert.Equal(["النص الأول", "النص المعدّل"], revisions.EnumerateArray().Select(r => r.GetProperty("body").GetString()).ToList());

        Assert.Equal(HttpStatusCode.PreconditionFailed, (await SendAsync(reem, HttpMethod.Put, $"/api/v1/comments/{id}", new { body = "قديم" }, comment.ETag)).StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionRequired, (await SendAsync(reem, HttpMethod.Put, $"/api/v1/comments/{id}", new { body = "بدون" }, null)).StatusCode);
        var notAuthor = await SendAsync(fahad, HttpMethod.Put, $"/api/v1/comments/{id}", new { body = "ليس تعليقي" }, edited.Headers.ETag);
        Assert.Equal(HttpStatusCode.Forbidden, notAuthor.StatusCode);
        Assert.Equal("Comment.NotAuthor", (await ProblemAsync(notAuthor)).Code);

        // Past the window (24 hours) the edit is refused.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE work.comments SET created_at = now() - interval '25 hours' WHERE id = {id}");
        }

        var (_, aged) = await GetCommentAsync(reem, item, id);
        var expired = await SendAsync(reem, HttpMethod.Put, $"/api/v1/comments/{id}", new { body = "متأخر" }, aged);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, expired.StatusCode);
        Assert.Equal("Comment.EditWindowExpired", (await ProblemAsync(expired)).Code);
    }

    private static async Task<(System.Text.Json.JsonElement Body, System.Net.Http.Headers.EntityTagHeaderValue ETag)> GetCommentAsync(HttpClient client, Guid item, Guid id)
    {
        var comment = (await ReadAsync(await client.GetAsync(new Uri($"/api/v1/comments?subjectType=work-item&subjectId={item}", UriKind.Relative))))
            .GetProperty("items").EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == id);
        return (comment, new System.Net.Http.Headers.EntityTagHeaderValue($"\"{comment.GetProperty("version").GetUInt32()}\""));
    }

    [Fact]
    public async Task Scenario_14_15_R15_R16_deleting_hides_the_text_and_is_for_the_author_or_a_holder_of_files_delete()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        using var fahad = await factory.SignedInClientAsync("fahad", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var (item, _, _) = await CreateAsync(admin, project);

        var mine = await AddAsync(reem, "work-item", item, "سأحذفه");
        var minePlain = mine.Body.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(reem, HttpMethod.Delete, $"/api/v1/comments/{minePlain}", null, mine.ETag)).StatusCode);

        var (deleted, deletedTag) = await GetCommentAsync(reem, item, minePlain);
        Assert.True(deleted.GetProperty("deleted").GetBoolean());
        Assert.Equal(string.Empty, deleted.GetProperty("body").GetString());
        Assert.Empty((await ReadAsync(await reem.GetAsync(new Uri($"/api/v1/comments/{minePlain}/revisions", UriKind.Relative)))).EnumerateArray());
        var twice = await SendAsync(reem, HttpMethod.Delete, $"/api/v1/comments/{minePlain}", null, deletedTag);
        Assert.Equal("Comment.Deleted", (await ProblemAsync(twice)).Code);
        var editDeleted = await SendAsync(reem, HttpMethod.Put, $"/api/v1/comments/{minePlain}", new { body = "x" }, deletedTag);
        Assert.Equal("Comment.Deleted", (await ProblemAsync(editDeleted)).Code);

        // Someone else's comment: a team member cannot remove it, the administrator can.
        var theirs = await AddAsync(reem, "work-item", item, "تعليق زميل");
        var theirsId = theirs.Body.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(fahad, HttpMethod.Delete, $"/api/v1/comments/{theirsId}", null, theirs.ETag)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(admin, HttpMethod.Delete, $"/api/v1/comments/{theirsId}", null, theirs.ETag)).StatusCode);

        var all = await ReadAsync(await fahad.GetAsync(new Uri($"/api/v1/comments?subjectType=work-item&subjectId={item}", UriKind.Relative)));
        Assert.Equal(2, all.GetProperty("total").GetInt32());
        Assert.All(all.GetProperty("items").EnumerateArray(), c => Assert.True(c.GetProperty("deleted").GetBoolean()));
        Assert.Equal(HttpStatusCode.NotFound, (await fahad.GetAsync(new Uri($"/api/v1/comments/{Guid.NewGuid()}/revisions", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task Demo_data_the_erp_item_and_project_have_the_sample_comments_oldest_first()
    {
        using var admin = await factory.AdminClientAsync();
        var erp = await ProjectByCodeAsync(admin, "ERP");
        var projectId = erp.GetProperty("id").GetGuid();
        var items = await ReadAsync(await admin.GetAsync(new Uri($"/api/v1/work-items?projectId={projectId}&q=ERP-104", UriKind.Relative)));
        var item = items.GetProperty("items")[0].GetProperty("id").GetGuid();

        var onItem = (await ReadAsync(await admin.GetAsync(new Uri($"/api/v1/comments?subjectType=work-item&subjectId={item}", UriKind.Relative)))).GetProperty("items");
        var onProject = (await ReadAsync(await admin.GetAsync(new Uri($"/api/v1/comments?subjectType=project&subjectId={projectId}", UriKind.Relative)))).GetProperty("items");

        Assert.True(onItem.GetArrayLength() >= 2);
        Assert.Contains("المالية", onItem[0].GetProperty("body").GetString());
        Assert.True(onProject.GetArrayLength() >= 1);
    }
}
