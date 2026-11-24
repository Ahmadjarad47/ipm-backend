using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ipm.IntegrationTests.Git;
using Ipm.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.SignalR.Client;
using static Ipm.IntegrationTests.Approvals.ApprovalApi;
using static Ipm.IntegrationTests.Identity.IdentityApi;
using static Ipm.IntegrationTests.Projects.ProjectApi;
using static Ipm.IntegrationTests.Work.WorkApi;

namespace Ipm.IntegrationTests.Activity;

/// <summary>Scenarios 7 to 14 of docs/modules/08-activity-audit-notifications.md: who is told what, reading, and the safety of the reaction.</summary>
[Collection(ApiCollection.Name)]
public sealed partial class NotificationTests(ApiFactory factory)
{
    private static async Task<List<JsonElement>> MineAsync(HttpClient client, Func<JsonElement, bool>? where = null) =>
        [.. (await GetAsync(client, "/api/v1/notifications?pageSize=100")).Body.GetProperty("items").EnumerateArray().Where(where ?? (_ => true))];

    private static Func<JsonElement, bool> Of(string kind, string? bodyHas = null, Guid? project = null) =>
        n => n.GetProperty("kind").GetString() == kind
            && (bodyHas is null || n.GetProperty("body").GetString()!.Contains(bodyHas, StringComparison.Ordinal))
            && (project is null || (n.GetProperty("projectId").ValueKind == JsonValueKind.String && n.GetProperty("projectId").GetGuid() == project));

    [Fact]
    public async Task Scenario_7_R11_assigning_and_moving_an_item_tells_the_assignees_but_not_the_actor()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var adminId = await MyIdAsync(admin);
        var reemId = await UserIdAsync(admin, "reem");
        var (itemId, _, item) = await CreateAsync(admin, project, assignees: [reemId, adminId]);
        var reference = item.GetProperty("ref").GetString()!;

        var assigned = await MineAsync(reem, Of("work-item.assigned", reference));
        Assert.Single(assigned);
        Assert.Equal($"/work-items/{itemId}", assigned[0].GetProperty("link").GetString());
        Assert.Empty(await MineAsync(admin, Of("work-item.assigned", reference)));

        await MoveAsync(admin, itemId, "in_progress");
        var moved = await MineAsync(reem, Of("work-item.status-changed", reference));
        Assert.Single(moved);
        Assert.Contains(reference, moved[0].GetProperty("body").GetString(), StringComparison.Ordinal);
        Assert.Empty(await MineAsync(admin, Of("work-item.status-changed", reference)));

        var (current, etag) = await GetItemAsync(admin, itemId);
        Assert.Equal(HttpStatusCode.OK, (await PutItemAsync(admin, itemId, UpdateBody(current, assignees: [adminId]), etag)).StatusCode);
        Assert.Single(await MineAsync(reem, Of("work-item.unassigned", reference)));
    }

    [Fact]
    public async Task Scenario_8_R11_a_new_member_is_told_and_a_removed_member_too()
    {
        using var admin = await factory.AdminClientAsync();
        using var fahad = await factory.SignedInClientAsync("fahad", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);

        var added = await MineAsync(fahad, Of("project.member-added", project: project));
        Assert.Single(added);
        Assert.Equal($"/projects/{project}", added[0].GetProperty("link").GetString());

        var adminId = await MyIdAsync(admin);
        var manager = await RoleIdAsync(admin, "project_manager");
        var (_, etag) = await GetProjectAsync(admin, project);
        Assert.Equal(HttpStatusCode.OK, (await PutMembersAsync(admin, project, MembersBody((adminId, manager)), etag)).StatusCode);
        Assert.Single(await MineAsync(fahad, Of("project.member-removed")), n => n.GetProperty("link").GetString() == "/projects");
    }

    [Fact]
    public async Task Scenario_9_R11_a_mention_tells_a_member_who_sees_the_project_and_nobody_else_and_an_edit_does_not_repeat_it()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        using var noura = await factory.SignedInClientAsync("noura", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var (itemId, _, _) = await CreateAsync(admin, project);
        var tag = Guid.NewGuid().ToString("N")[..8];

        var created = await admin.PostAsJsonAsync(new Uri("/api/v1/comments", UriKind.Relative), new { subjectType = "work-item", subjectId = itemId, body = $"{tag} @reem @noura انظر" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var comment = await ReadAsync(created);

        Assert.Single(await MineAsync(reem, Of("comment.mentioned", tag)));
        Assert.Empty(await MineAsync(noura, Of("comment.mentioned", tag)));

        var edited = await SendAsync(admin, HttpMethod.Put, $"/api/v1/comments/{comment.GetProperty("id").GetGuid()}", new { body = $"{tag} @reem تعديل" }, created.Headers.ETag);
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.Empty(await MineAsync(reem, n => n.GetProperty("kind").GetString() == "comment.mentioned" && n.GetProperty("body").GetString()!.Contains("تعديل", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Scenario_10_R11_the_approval_flow_tells_the_approvers_and_then_the_requester()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var gated = await GatedItemAsync(admin);

        var (id, _, body) = await SubmitAsync(reem, gated.Template, "work-item", gated.Item);
        var title = body.GetProperty("title").GetString()!;

        Assert.Contains(await MineAsync(admin), n => Of("approval.requested", title)(n) && n.GetProperty("link").GetString() == $"/approvals/{id}");
        Assert.DoesNotContain(await MineAsync(reem), n => Of("approval.requested", title)(n));

        var (_, etag) = await GetApprovalAsync(admin, id);
        var remind = await reem.PostAsync(new Uri($"/api/v1/approvals/{id}/remind", UriKind.Relative), null);
        Assert.Equal(HttpStatusCode.OK, remind.StatusCode);
        Assert.Contains(await MineAsync(admin), n => Of("approval.reminded", title)(n));

        Assert.Equal(HttpStatusCode.OK, (await ApproveAllAsync(admin, id)).StatusCode);
        Assert.Contains(await MineAsync(reem), n => n.GetProperty("kind").GetString() == "approval.decided" && n.GetProperty("body").GetString() == title);
        Assert.NotNull(etag);
    }

    [Fact]
    public async Task Scenario_11_R11_a_merged_pull_request_tells_the_assignees_once_each()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var reemId = await UserIdAsync(admin, "reem");
        var (repo, _) = await GitApi.ConnectAsync(admin, project);
        var (_, _, one) = await CreateAsync(admin, project, assignees: [reemId]);
        var (_, _, two) = await CreateAsync(admin, project, assignees: [reemId]);
        var refs = $"{one.GetProperty("ref").GetString()} {two.GetProperty("ref").GetString()}";

        var response = await GitApi.DeliverAsync(factory, "gitea", repo, GitApi.PrBody(9, $"{refs} done", action: "closed", merged: true), eventName: "pull_request");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // one notification for each item the user is assigned to, none for the webhook caller
        Assert.Equal(2, (await MineAsync(reem, Of("git.pr-merged", project: project))).Count);
    }

    [Fact]
    public async Task Scenario_12_R12_reading_marks_counts_and_stays_private()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var reemId = await UserIdAsync(admin, "reem");
        await CreateAsync(admin, project, assignees: [reemId]);
        await CreateAsync(admin, project, assignees: [reemId]);

        var page = (await GetAsync(reem, "/api/v1/notifications?unread=true&pageSize=100")).Body;
        var unread = page.GetProperty("unread").GetInt32();
        Assert.True(unread >= 2);
        Assert.All(page.GetProperty("items").EnumerateArray(), n => Assert.False(n.GetProperty("read").GetBoolean()));
        Assert.Equal(unread, (await GetAsync(reem, "/api/v1/notifications/unread-count")).Body.GetProperty("unread").GetInt32());

        var mine = (await MineAsync(reem, Of("work-item.assigned", project: project)))[0];
        var id = mine.GetProperty("id").GetGuid();
        var first = await reem.PatchAsync(new Uri($"/api/v1/notifications/{id}/read", UriKind.Relative), null);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var readAt = (await ReadAsync(first)).GetProperty("readAt").GetDateTimeOffset();
        await Task.Delay(30);
        var again = await ReadAsync(await reem.PatchAsync(new Uri($"/api/v1/notifications/{id}/read", UriKind.Relative), null));
        Assert.True(Math.Abs((readAt - again.GetProperty("readAt").GetDateTimeOffset()).TotalMilliseconds) < 1);
        Assert.Equal(unread - 1, (await GetAsync(reem, "/api/v1/notifications/unread-count")).Body.GetProperty("unread").GetInt32());

        // somebody else's notification does not exist for you
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PatchAsync(new Uri($"/api/v1/notifications/{id}/read", UriKind.Relative), null)).StatusCode);
        Assert.DoesNotContain(await MineAsync(admin), n => n.GetProperty("id").GetGuid() == id);

        // reading a project clears only that project, then everything
        Assert.Equal(HttpStatusCode.NoContent, (await reem.PostAsync(new Uri($"/api/v1/notifications/read-all?projectId={project}", UriKind.Relative), null)).StatusCode);
        Assert.Empty(await MineAsync(reem, n => Of("work-item.assigned", project: project)(n) && !n.GetProperty("read").GetBoolean()));
        Assert.Equal(HttpStatusCode.NoContent, (await reem.PostAsync(new Uri("/api/v1/notifications/read-all", UriKind.Relative), null)).StatusCode);
        Assert.Equal(0, (await GetAsync(reem, "/api/v1/notifications/unread-count")).Body.GetProperty("unread").GetInt32());
    }

    [Fact]
    public async Task Scenario_13_R10_a_failing_reaction_neither_fails_nor_undoes_the_request()
    {
        using var admin = await factory.AdminClientAsync();
        var (project, _) = await NewProjectAsync(admin);
        var (itemId, _, _) = await CreateAsync(admin, project, assignees: [await UserIdAsync(admin, "reem")]);

        FailingStatusHandler.Fail = true;
        try
        {
            await MoveAsync(admin, itemId, "in_progress");
        }
        finally
        {
            FailingStatusHandler.Fail = false;
        }

        var (current, _) = await GetItemAsync(admin, itemId);
        Assert.Equal(await StatusIdAsync(admin, "in_progress"), current.GetProperty("statusId").GetGuid());
    }

    [Fact]
    public async Task Scenario_14_R13_links_are_relative_paths_built_from_ids_and_text_is_clean()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var reemId = await UserIdAsync(admin, "reem");
        await CreateAsync(admin, project, assignees: [reemId]);

        var all = await MineAsync(reem);
        Assert.NotEmpty(all);
        Assert.All(all, n =>
        {
            Assert.Matches(SafeLink(), n.GetProperty("link").GetString()!);
            Assert.DoesNotContain(n.GetProperty("title").GetString()!, c => char.IsControl(c));
            Assert.True(n.GetProperty("title").GetString()!.Length <= 200 && n.GetProperty("body").GetString()!.Length <= 500);
        });
    }

    [Fact]
    public async Task Seed_gives_each_main_demo_user_five_sample_notifications()
    {
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);

        var samples = (await GetAsync(reem, "/api/v1/notifications?pageSize=100")).Body.GetProperty("items").EnumerateArray().Where(n => n.GetProperty("title").GetString() == "أُضفت إلى مشروع" && n.GetProperty("body").GetString() == "نظام تخطيط الموارد").ToList();
        Assert.Single(samples);
        Assert.True(samples[0].GetProperty("read").GetBoolean());
    }

    [Fact]
    public async Task Scenario_15_R12_several_notifications_are_marked_read_in_one_call()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        var reemId = await UserIdAsync(admin, "reem");
        await CreateAsync(admin, project, assignees: [reemId]);
        await CreateAsync(admin, project, assignees: [reemId]);

        var mine = await MineAsync(reem, Of("work-item.assigned", project: project));
        Assert.Equal(2, mine.Count);
        var ids = mine.Select(n => n.GetProperty("id").GetGuid()).ToArray();

        // somebody else's id in the same batch is silently skipped, not an error
        var otherId = (await MineAsync(admin)).Select(n => n.GetProperty("id").GetGuid()).FirstOrDefault();
        var batch = await reem.PostAsJsonAsync(new Uri("/api/v1/notifications/read-batch", UriKind.Relative), new { ids = ids.Append(otherId) });
        Assert.Equal(HttpStatusCode.NoContent, batch.StatusCode);

        var after = await MineAsync(reem, Of("work-item.assigned", project: project));
        Assert.All(after, n => Assert.True(n.GetProperty("read").GetBoolean()));

        // reading again is harmless
        Assert.Equal(HttpStatusCode.NoContent, (await reem.PostAsJsonAsync(new Uri("/api/v1/notifications/read-batch", UriKind.Relative), new { ids })).StatusCode);
    }

    [Fact]
    public async Task Scenario_16_R11_a_new_notification_is_pushed_live_over_the_notifications_hub()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        var reemToken = reem.DefaultRequestHeaders.Authorization!.Parameter!;

        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "/api/v1/hubs/notifications"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(reemToken);
            })
            .Build();

        var received = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<JsonElement>("notificationReceived", n => received.TrySetResult(n));
        await connection.StartAsync();

        var (project, _) = await NewProjectAsync(admin);
        var reemId = await UserIdAsync(admin, "reem");
        var (itemId, _, item) = await CreateAsync(admin, project, assignees: [reemId]);
        var reference = item.GetProperty("ref").GetString()!;

        var pushed = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("work-item.assigned", pushed.GetProperty("kind").GetString());
        Assert.Contains(reference, pushed.GetProperty("body").GetString(), StringComparison.Ordinal);
        Assert.Equal($"/work-items/{itemId}", pushed.GetProperty("link").GetString());

        // the push only ever follows the commit: by the time it arrives the row is already readable over REST too
        Assert.Contains(await MineAsync(reem), n => n.GetProperty("id").GetGuid() == pushed.GetProperty("id").GetGuid());
    }

    [GeneratedRegex(@"^/(work-items/[0-9a-f-]{36}|approvals(/[0-9a-f-]{36})?|projects(/[0-9a-f-]{36})?)$")]
    private static partial Regex SafeLink();
}
