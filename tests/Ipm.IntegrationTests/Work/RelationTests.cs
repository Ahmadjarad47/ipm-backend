using System.Net;
using System.Net.Http.Json;
using Ipm.IntegrationTests.Config;
using Ipm.IntegrationTests.Infrastructure;
using static Ipm.IntegrationTests.Identity.IdentityApi;
using static Ipm.IntegrationTests.Projects.ProjectApi;
using static Ipm.IntegrationTests.Work.WorkApi;

namespace Ipm.IntegrationTests.Work;

/// <summary>Scenarios 1 to 11 of docs/modules/05b-relations-comments.md: links between work items.</summary>
[Collection(ApiCollection.Name)]
public sealed class RelationTests(ApiFactory factory)
{
    private static async Task<Guid> KindAsync(HttpClient client, string key) =>
        (await ConfigApi.RelationTypeByKeyAsync(client, key)).GetProperty("id").GetGuid();

    private static Task<HttpResponseMessage> LinkAsync(HttpClient client, Guid source, Guid target, Guid kind) =>
        client.PostAsJsonAsync(new Uri("/api/v1/relations", UriKind.Relative), new { sourceId = source, targetId = target, relationTypeId = kind });

    [Fact]
    public async Task Scenario_1_a_member_links_two_items_and_both_sides_read_it_with_their_own_name()
    {
        using var admin = await factory.AdminClientAsync();
        using var fahad = await factory.SignedInClientAsync("fahad", ApiFactory.DemoPassword);
        using var abdullah = await factory.SignedInClientAsync("abdullah", ApiFactory.DemoPassword);
        using var noura = await factory.SignedInClientAsync("noura", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var (a, _, _) = await CreateAsync(admin, project, title: "أ");
        var (b, _, _) = await CreateAsync(admin, project, title: "ب");
        var blocks = await KindAsync(admin, "blocks");

        var created = await LinkAsync(fahad, a, b, blocks);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var relationId = (await ReadAsync(created)).GetProperty("id").GetGuid();

        var fromSource = (await ReadAsync(await fahad.GetAsync(new Uri($"/api/v1/work-items/{a}/relations", UriKind.Relative))))[0];
        Assert.True(fromSource.GetProperty("outgoing").GetBoolean());
        Assert.Equal("يحجب", fromSource.GetProperty("label").GetString());
        Assert.Equal(b, fromSource.GetProperty("other").GetProperty("id").GetGuid());
        var fromTarget = (await ReadAsync(await fahad.GetAsync(new Uri($"/api/v1/work-items/{b}/relations", UriKind.Relative))))[0];
        Assert.False(fromTarget.GetProperty("outgoing").GetBoolean());
        Assert.Equal("محجوب بواسطة", fromTarget.GetProperty("label").GetString());

        Assert.Equal(1, (await ReadAsync(await abdullah.GetAsync(new Uri($"/api/v1/projects/{project}/relations", UriKind.Relative)))).GetArrayLength());
        Assert.Equal(HttpStatusCode.Forbidden, (await LinkAsync(abdullah, b, a, await KindAsync(admin, "relates"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await LinkAsync(noura, a, b, blocks)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await noura.GetAsync(new Uri($"/api/v1/work-items/{a}/relations", UriKind.Relative))).StatusCode);

        var (_, _, self) = await CreateAsync(admin, project, title: "ج");
        Assert.Equal("Relation.SelfLink", (await ProblemAsync(await LinkAsync(admin, self.GetProperty("id").GetGuid(), self.GetProperty("id").GetGuid(), blocks))).Code);

        // Only a holder of workitem.relations.delete may remove it.
        Assert.Equal(HttpStatusCode.Forbidden, (await fahad.DeleteAsync(new Uri($"/api/v1/relations/{relationId}", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync(new Uri($"/api/v1/relations/{relationId}", UriKind.Relative))).StatusCode);
        Assert.Empty((await ReadAsync(await fahad.GetAsync(new Uri($"/api/v1/work-items/{a}/relations", UriKind.Relative)))).EnumerateArray());
    }

    [Fact]
    public async Task Scenario_3_and_4_R4_R5_duplicates_and_cycles_are_refused()
    {
        using var admin = await factory.AdminClientAsync();
        var (project, _) = await NewProjectAsync(admin);
        var (a, _, _) = await CreateAsync(admin, project, title: "أ");
        var (b, _, _) = await CreateAsync(admin, project, title: "ب");
        var (c, _, _) = await CreateAsync(admin, project, title: "ج");
        var blocks = await KindAsync(admin, "blocks");
        var relates = await KindAsync(admin, "relates");

        Assert.Equal(HttpStatusCode.Created, (await LinkAsync(admin, a, b, blocks)).StatusCode);
        var again = await LinkAsync(admin, a, b, blocks);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("Relation.Duplicate", (await ProblemAsync(again)).Code);
        Assert.Equal(HttpStatusCode.Conflict, (await LinkAsync(admin, b, a, blocks)).StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await LinkAsync(admin, b, c, blocks)).StatusCode);
        var cycle = await LinkAsync(admin, c, a, blocks);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, cycle.StatusCode);
        Assert.Equal("Relation.Cycle", (await ProblemAsync(cycle)).Code);

        // A type that allows cycles accepts the same shape, but a link and its inverse are still one link.
        Assert.Equal(HttpStatusCode.Created, (await LinkAsync(admin, a, b, relates)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await LinkAsync(admin, b, c, relates)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await LinkAsync(admin, c, a, relates)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await LinkAsync(admin, b, a, relates)).StatusCode);
    }

    [Fact]
    public async Task Scenario_2_R3_the_allowed_pairs_of_the_type_decide_which_items_can_be_linked()
    {
        using var admin = await factory.AdminClientAsync();
        var (project, _) = await NewProjectAsync(admin);
        var (task, _, _) = await CreateAsync(admin, project, "task", "مهمة");
        var (bug, _, _) = await CreateAsync(admin, project, "bug", "خطأ");
        var (other, _, _) = await CreateAsync(admin, project, "task", "مهمة ثانية");
        var taskType = await TypeIdAsync(admin, "task");
        var bugType = await TypeIdAsync(admin, "bug");

        async Task<Guid> PairedAsync(bool directed)
        {
            var (id, etag) = await ConfigApi.CreateRelationTypeAsync(admin, ConfigApi.NewKey("rp"), directed);
            var body = new
            {
                name = "يرتبط", inverseName = directed ? "مرتبط عكسياً" : "يرتبط", directed, acyclic = false, active = true,
                allowedPairs = new[] { new { sourceTypeId = taskType, targetTypeId = bugType } },
            };
            Assert.Equal(HttpStatusCode.OK, (await SendAsync(admin, HttpMethod.Put, $"/api/v1/relation-types/{id}", body, etag)).StatusCode);
            return id;
        }

        var directedKind = await PairedAsync(true);
        var refused = await LinkAsync(admin, task, other, directedKind);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Equal("Relation.PairNotAllowed", (await ProblemAsync(refused)).Code);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await LinkAsync(admin, bug, task, directedKind)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await LinkAsync(admin, task, bug, directedKind)).StatusCode);

        var undirectedKind = await PairedAsync(false);
        Assert.Equal(HttpStatusCode.Created, (await LinkAsync(admin, bug, task, undirectedKind)).StatusCode);
    }

    [Fact]
    public async Task Scenario_5_6_R1_R2_R6_a_switched_off_type_other_projects_archived_items_and_stale_versions_are_refused()
    {
        using var admin = await factory.AdminClientAsync();
        var (project, _) = await NewProjectAsync(admin);
        var (other, _) = await NewProjectAsync(admin);
        var (a, aEtag, _) = await CreateAsync(admin, project, title: "أ");
        var (b, _, _) = await CreateAsync(admin, project, title: "ب");
        var (foreign, _, _) = await CreateAsync(admin, other, title: "من مشروع ثاني");
        var (gone, goneEtag, _) = await CreateAsync(admin, project, title: "ستؤرشف");
        var blocks = await KindAsync(admin, "blocks");

        var (kind, kindEtag) = await ConfigApi.CreateRelationTypeAsync(admin, ConfigApi.NewKey("ri"));
        var off = await SendAsync(admin, HttpMethod.Put, $"/api/v1/relation-types/{kind}", new { name = "يرتبط", inverseName = "عكسي", directed = true, acyclic = false, active = false, allowedPairs = Array.Empty<object>() }, kindEtag);
        Assert.Equal(HttpStatusCode.OK, off.StatusCode);
        var inactive = await LinkAsync(admin, a, b, kind);
        Assert.Equal("Relation.TypeInactive", (await ProblemAsync(inactive)).Code);

        var crossProject = await LinkAsync(admin, a, foreign, blocks);
        Assert.Equal("Relation.CrossProjectNotAllowed", (await ProblemAsync(crossProject)).Code);

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(admin, HttpMethod.Delete, $"/api/v1/work-items/{gone}", null, goneEtag)).StatusCode);
        Assert.Equal("Relation.ItemArchived", (await ProblemAsync(await LinkAsync(admin, a, gone, blocks))).Code);

        var stale = await admin.PostAsJsonAsync(
            new Uri("/api/v1/relations", UriKind.Relative), new { sourceId = a, targetId = b, relationTypeId = blocks, sourceVersion = 1U });
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        var fresh = await admin.PostAsJsonAsync(
            new Uri("/api/v1/relations", UriKind.Relative), new { sourceId = a, targetId = b, relationTypeId = blocks, sourceVersion = uint.Parse(aEtag.Tag.Trim((char)34), System.Globalization.CultureInfo.InvariantCulture) });
        Assert.Equal(HttpStatusCode.Created, fresh.StatusCode);

        var (_, projectEtag) = await GetProjectAsync(admin, project);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(admin, HttpMethod.Post, $"/api/v1/projects/{project}/archive", null, projectEtag)).StatusCode);
        var relationId = (await ReadAsync(fresh)).GetProperty("id").GetGuid();
        var inArchived = await admin.DeleteAsync(new Uri($"/api/v1/relations/{relationId}", UriKind.Relative));
        Assert.Equal("Relation.ProjectArchived", (await ProblemAsync(inArchived)).Code);
    }

    [Fact]
    public async Task Scenario_9_10_R9_R10_an_open_blocker_stops_the_closing_move_and_the_list_filters_follow_the_links()
    {
        using var admin = await factory.AdminClientAsync();
        var (project, _) = await NewProjectAsync(admin);
        var fahad = await UserIdAsync(admin, "fahad");
        var (blocker, _, _) = await CreateAsync(admin, project, title: "الحاجب", assignees: [fahad]);
        var (blocked, _, _) = await CreateAsync(admin, project, title: "المحجوب", assignees: [fahad]);
        var (free, _, _) = await CreateAsync(admin, project, title: "حر");
        Assert.Equal(HttpStatusCode.Created, (await LinkAsync(admin, blocker, blocked, await KindAsync(admin, "blocks"))).StatusCode);
        await MoveAsync(admin, blocked, "in_progress", "in_review");

        var (_, etag) = await GetItemAsync(admin, blocked);
        var refused = await TransitionItemAsync(admin, blocked, await StatusIdAsync(admin, "completed"), etag);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        var problem = await ProblemAsync(refused);
        Assert.Equal("WorkItem.BlockedByDependencies", problem.Code);
        Assert.Contains((await GetItemAsync(admin, blocker)).Body.GetProperty("ref").GetString()!, problem.Detail);

        async Task<List<Guid>> Ids(string query) =>
            (await ReadAsync(await admin.GetAsync(new Uri($"/api/v1/work-items?projectId={project}&pageSize=200&{query}", UriKind.Relative))))
                .GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();

        Assert.Equal([blocked], await Ids($"blockedBy={blocker}"));
        Assert.Equal([blocker], await Ids($"blocking={blocked}"));
        Assert.Equal([blocked], await Ids("unblocked=false"));
        Assert.DoesNotContain(blocked, await Ids("unblocked=true"));
        Assert.Equal([free], await Ids("hasDependency=false"));
        Assert.Equal(2, (await Ids("hasDependency=true")).Count);

        // Cancelling the blocker lifts it: the move and the bulk move both work.
        await MoveAsync(admin, blocker, "in_progress");
        var (_, blockerEtag) = await GetItemAsync(admin, blocker);
        Assert.Equal(HttpStatusCode.OK, (await TransitionItemAsync(admin, blocker, await StatusIdAsync(admin, "cancelled"), blockerEtag)).StatusCode);
        Assert.Equal(2, (await Ids("unblocked=true&hasDependency=true")).Count);
        Assert.Empty(await Ids("unblocked=false"));
        var closed = await BulkAsync(admin, [blocked], new { kind = "transition", toStatusId = await StatusIdAsync(admin, "completed") });
        Assert.True(closed[0].GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task Scenario_11_R11_a_type_in_use_keeps_its_direction_and_cannot_lose_pairs_under_a_link()
    {
        using var admin = await factory.AdminClientAsync();
        var (project, _) = await NewProjectAsync(admin);
        var (task, _, _) = await CreateAsync(admin, project, "task", "مهمة");
        var (bug, _, _) = await CreateAsync(admin, project, "bug", "خطأ");
        var taskType = await TypeIdAsync(admin, "task");
        var bugType = await TypeIdAsync(admin, "bug");
        var subtaskType = await TypeIdAsync(admin, "subtask");
        var (kind, etag) = await ConfigApi.CreateRelationTypeAsync(admin, ConfigApi.NewKey("rl"));

        object Body(bool directed, bool acyclic, params (Guid S, Guid T)[] pairs) => new
        {
            name = "يرتبط", inverseName = directed ? "عكسي" : "يرتبط", directed, acyclic, active = true,
            allowedPairs = pairs.Select(p => new { sourceTypeId = p.S, targetTypeId = p.T }).ToArray(),
        };

        // Free to change while nothing uses the type.
        var ok = await SendAsync(admin, HttpMethod.Put, $"/api/v1/relation-types/{kind}", Body(true, false, (taskType, bugType)), etag);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await LinkAsync(admin, task, bug, kind)).StatusCode);

        var (_, current) = await GetAsync(admin, $"/api/v1/relation-types/{kind}");
        var direction = await SendAsync(admin, HttpMethod.Put, $"/api/v1/relation-types/{kind}", Body(false, false, (taskType, bugType)), current);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, direction.StatusCode);
        Assert.Equal("RelationType.LockedAfterUse", (await ProblemAsync(direction)).Code);
        var acyclic = await SendAsync(admin, HttpMethod.Put, $"/api/v1/relation-types/{kind}", Body(true, true, (taskType, bugType)), current);
        Assert.Equal("RelationType.LockedAfterUse", (await ProblemAsync(acyclic)).Code);

        var narrowed = await SendAsync(admin, HttpMethod.Put, $"/api/v1/relation-types/{kind}", Body(true, false, (subtaskType, bugType)), current);
        Assert.Equal("RelationType.LockedAfterUse", (await ProblemAsync(narrowed)).Code);

        var widened = await SendAsync(admin, HttpMethod.Put, $"/api/v1/relation-types/{kind}", Body(true, false, (taskType, bugType), (subtaskType, bugType)), current);
        Assert.Equal(HttpStatusCode.OK, widened.StatusCode);
    }

    [Fact]
    public async Task Demo_data_the_erp_project_has_the_three_sample_links()
    {
        using var admin = await factory.AdminClientAsync();
        var erp = (await ProjectByCodeAsync(admin, "ERP")).GetProperty("id").GetGuid();

        var relations = await ReadAsync(await admin.GetAsync(new Uri($"/api/v1/projects/{erp}/relations", UriKind.Relative)));

        Assert.True(relations.GetArrayLength() >= 3);
    }
}
