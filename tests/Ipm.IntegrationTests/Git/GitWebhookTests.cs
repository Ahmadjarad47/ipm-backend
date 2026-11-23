using System.Net;
using System.Text;
using System.Text.Json;
using Ipm.Infrastructure.Persistence;
using Ipm.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Ipm.IntegrationTests.Git.GitApi;
using static Ipm.IntegrationTests.Identity.IdentityApi;
using static Ipm.IntegrationTests.Work.WorkApi;

namespace Ipm.IntegrationTests.Git;

/// <summary>Scenarios 2, 5, 6, 7, 8, 9 and 13 of docs/modules/07-git-integration.md: signed deliveries, replay, idempotency, pull requests, failures.</summary>
[Collection(ApiCollection.Name)]
public sealed class GitWebhookTests(ApiFactory factory)
{
    private async Task<(HttpClient Admin, Guid Project, Guid Repo, string Ref)> SetupAsync(string provider = "gitea")
    {
        var admin = await factory.AdminClientAsync();
        var (project, _) = await NewProjectAsync(admin);
        var (repo, _) = await ConnectAsync(admin, project, provider);
        var (_, _, item) = await CreateAsync(admin, project);
        return (admin, project, repo, item.GetProperty("ref").GetString()!);
    }

    [Theory]
    [InlineData("gitea")]
    [InlineData("github")]
    [InlineData("gitlab")]
    public async Task Scenario_2_R5_a_signed_push_links_the_item_for_every_provider(string provider)
    {
        var (admin, project, repo, reference) = await SetupAsync(provider);
        using var _ = admin;

        var response = await DeliverAsync(factory, provider, repo, PushBody($"[{reference}] fix the thing", branch: "feature/x"), eventName: "push");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal("processed", body.GetProperty("outcome").GetString());
        Assert.Contains(reference, body.GetProperty("linkedRefs").ToString(), StringComparison.Ordinal);

        var events = await EventsAsync(admin, project, reference);
        Assert.Equal(1, events.GetProperty("items").GetArrayLength());
        Assert.Equal("push", events.GetProperty("items")[0].GetProperty("kind").GetString());
    }

    [Fact]
    public async Task Scenario_2_R5_a_bad_or_missing_signature_headers_or_a_changed_body_are_refused_and_logged()
    {
        var (admin, project, repo, reference) = await SetupAsync();
        using var _ = admin;
        var body = PushBody($"{reference} work");

        Assert.Equal(HttpStatusCode.Unauthorized, (await DeliverAsync(factory, "gitea", repo, body, signature: "deadbeef")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await DeliverAsync(factory, "gitea", repo, body, secret: "another-secret-value-123456")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await DeliverAsync(factory, "gitea", repo, body, omitDelivery: true)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await DeliverAsync(factory, "gitea", Guid.NewGuid(), body)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await DeliverAsync(factory, "unknown", repo, body)).StatusCode);

        var rejected = (await DeliveriesAsync(admin, project)).GetProperty("items").EnumerateArray().Where(d => d.GetProperty("outcome").GetString() == "rejected").ToList();
        Assert.Equal(3, rejected.Count);
        Assert.Equal(0, (await EventsAsync(admin, project)).GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Scenario_5_R5_a_delivery_outside_the_five_minute_window_is_a_replay()
    {
        var (admin, _, repo, reference) = await SetupAsync();
        using var _ = admin;
        var body = PushBody($"{reference} work");

        var old = await DeliverAsync(factory, "gitea", repo, body, timestamp: Stamp(secondsAgo: 600));
        Assert.Equal(HttpStatusCode.Unauthorized, old.StatusCode);
        Assert.Equal("Git.StaleDelivery", (await ReadAsync(old)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await DeliverAsync(factory, "gitea", repo, body, timestamp: Stamp(secondsAgo: -600))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await DeliverAsync(factory, "gitea", repo, body, timestamp: Stamp(secondsAgo: 120))).StatusCode);
    }

    [Fact]
    public async Task Scenario_6_R6_the_same_delivery_id_is_handled_once_and_a_rejected_one_does_not_burn_it()
    {
        var (admin, project, repo, reference) = await SetupAsync();
        using var _ = admin;
        var body = PushBody($"{reference} work");

        Assert.Equal(HttpStatusCode.Unauthorized, (await DeliverAsync(factory, "gitea", repo, body, deliveryId: "same-1", signature: "bad")).StatusCode);
        Assert.Equal("processed", (await ReadAsync(await DeliverAsync(factory, "gitea", repo, body, deliveryId: "same-1"))).GetProperty("outcome").GetString());
        Assert.Equal("duplicate", (await ReadAsync(await DeliverAsync(factory, "gitea", repo, body, deliveryId: "same-1"))).GetProperty("outcome").GetString());

        Assert.Equal(1, (await EventsAsync(admin, project)).GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Scenario_6_R6_a_body_over_one_megabyte_is_refused()
    {
        var (admin, _, repo, _) = await SetupAsync();
        using var _ = admin;

        var huge = PushBody(new string('x', 1_100_000));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await DeliverAsync(factory, "gitea", repo, huge)).StatusCode);
    }

    [Fact]
    public async Task Scenario_7_R7_only_real_items_of_the_project_are_linked_and_every_commit_is_stored_once()
    {
        var (admin, project, repo, reference) = await SetupAsync();
        using var _ = admin;
        var sha = Guid.NewGuid().ToString("N") + "00000000";

        var response = await ReadAsync(await DeliverAsync(factory, "gitea", repo, PushBody($"{reference} and ZZZ-999 and {reference} again", sha: sha)));
        Assert.Equal(1, response.GetProperty("linkedRefs").GetArrayLength());

        await DeliverAsync(factory, "gitea", repo, PushBody($"{reference} again", sha: sha));
        var events = (await EventsAsync(admin, project)).GetProperty("items");
        Assert.Equal(1, events.GetArrayLength());
        Assert.Equal(sha, events[0].GetProperty("sha").GetString());
    }

    [Fact]
    public async Task Scenario_8_R7_a_pull_request_is_one_record_that_moves_state_and_keeps_manual_links()
    {
        var (admin, project, repo, reference) = await SetupAsync();
        using var _ = admin;
        var (_, _, other) = await CreateAsync(admin, project);
        var otherRef = other.GetProperty("ref").GetString()!;

        var (_, itemBefore) = await GetItemByRefAsync(admin, project, reference);
        var statusBefore = itemBefore.GetProperty("statusId").GetGuid();
        await DeliverAsync(factory, "gitea", repo, PrBody(7, $"{reference} new screen"), eventName: "pull_request");
        var first = (await EventsAsync(admin, project, kind: "pull-request")).GetProperty("items");
        Assert.Equal(1, first.GetArrayLength());
        Assert.Equal("open", first[0].GetProperty("prState").GetString());
        var eventId = first[0].GetProperty("id").GetGuid();

        var link = await admin.PostAsync(new Uri($"/api/v1/git-events/{eventId}/link", UriKind.Relative), JsonBody(new { @ref = otherRef }));
        Assert.Equal(HttpStatusCode.OK, link.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync(new Uri($"/api/v1/git-events/{eventId}/link", UriKind.Relative), JsonBody(new { @ref = otherRef }))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync(new Uri($"/api/v1/git-events/{eventId}/link", UriKind.Relative), JsonBody(new { @ref = "NOPE-1" }))).StatusCode);

        await DeliverAsync(factory, "gitea", repo, PrBody(7, $"{reference} new screen", action: "closed", merged: true), eventName: "pull_request");
        var merged = (await EventsAsync(admin, project, kind: "merge")).GetProperty("items");
        Assert.Equal(1, merged.GetArrayLength());
        Assert.Equal(eventId, merged[0].GetProperty("id").GetGuid());
        Assert.Contains(otherRef, merged[0].GetProperty("workItemRefs").ToString(), StringComparison.Ordinal);

        // R8: merging never moves the item
        var (_, itemAfter) = await GetItemByRefAsync(admin, project, reference);
        Assert.Equal(statusBefore, itemAfter.GetProperty("statusId").GetGuid());

        var unlink = await admin.PostAsync(new Uri($"/api/v1/git-events/{eventId}/unlink", UriKind.Relative), JsonBody(new { @ref = otherRef }));
        Assert.Equal(HttpStatusCode.OK, unlink.StatusCode);
        Assert.DoesNotContain(otherRef, (await ReadAsync(unlink)).GetProperty("workItemRefs").ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Scenario_9_R9_a_failed_delivery_keeps_its_body_and_can_be_redelivered_once_fixed()
    {
        var (admin, project, repo, reference) = await SetupAsync();
        using var _ = admin;

        var failed = await DeliverAsync(factory, "gitea", repo, "this is not json", deliveryId: "bad-json-1");
        Assert.Equal(HttpStatusCode.OK, failed.StatusCode);
        Assert.Equal("failed", (await ReadAsync(failed)).GetProperty("outcome").GetString());

        var delivery = (await DeliveriesAsync(admin, project)).GetProperty("items").EnumerateArray().Single(d => d.GetProperty("outcome").GetString() == "failed");
        var deliveryId = delivery.GetProperty("id").GetGuid();
        Assert.DoesNotContain("payload", delivery.ToString(), StringComparison.OrdinalIgnoreCase);

        // still broken: redelivery fails again but is allowed
        var again = await admin.PostAsync(new Uri($"/api/v1/git/deliveries/{deliveryId}/redeliver", UriKind.Relative), null);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal("failed", (await ReadAsync(again)).GetProperty("outcome").GetString());

        // the stored body is fixed in the database, as if the bug in the handling were fixed
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE git.webhook_deliveries SET payload = {PushBody($"{reference} fixed")} WHERE id = {deliveryId}");
        }

        var fixedUp = await admin.PostAsync(new Uri($"/api/v1/git/deliveries/{deliveryId}/redeliver", UriKind.Relative), null);
        Assert.Equal(HttpStatusCode.OK, fixedUp.StatusCode);
        Assert.Equal("processed", (await ReadAsync(fixedUp)).GetProperty("outcome").GetString());
        Assert.Equal(1, (await EventsAsync(admin, project, reference)).GetProperty("items").GetArrayLength());

        // a processed delivery cannot be redelivered
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PostAsync(new Uri($"/api/v1/git/deliveries/{deliveryId}/redeliver", UriKind.Relative), null)).StatusCode);
    }

    [Fact]
    public async Task Scenario_13_R15_the_log_and_the_database_never_hold_a_secret_in_the_clear()
    {
        var (admin, project, repo, _) = await SetupAsync();
        using var _ = admin;
        await DeliverAsync(factory, "gitea", repo, PushBody("hello"));

        Assert.DoesNotContain(Secret, (await DeliveriesAsync(admin, project)).ToString(), StringComparison.Ordinal);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cipher = await db.Database.SqlQuery<string>($"SELECT webhook_secret_cipher AS \"Value\" FROM git.repositories WHERE id = {repo}").SingleAsync();
        Assert.DoesNotContain(Secret, cipher, StringComparison.Ordinal);
        var audit = await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM public.audit_log WHERE entity_id = {repo} AND (before::text LIKE '%cipher%' OR after::text LIKE '%cipher%')").SingleAsync();
        Assert.Equal(0, audit);
    }

    private static StringContent JsonBody(object value) => new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

    private static async Task<(Guid Id, JsonElement Body)> GetItemByRefAsync(HttpClient client, Guid project, string reference)
    {
        var list = (await GetAsync(client, $"/api/v1/work-items?projectId={project}&search={reference}")).Body.GetProperty("items");
        var item = list.EnumerateArray().Single(i => i.GetProperty("ref").GetString() == reference);
        return (item.GetProperty("id").GetGuid(), item);
    }
}
