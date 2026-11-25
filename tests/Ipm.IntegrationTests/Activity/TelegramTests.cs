using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ipm.Application.Abstractions.Caching;
using Ipm.Application.Abstractions.Telegram;
using Ipm.Domain.Activity;
using Ipm.Infrastructure.Persistence;
using Ipm.Infrastructure.Telegram;
using Ipm.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Ipm.IntegrationTests.Identity.IdentityApi;
using static Ipm.IntegrationTests.Projects.ProjectApi;
using static Ipm.IntegrationTests.Work.WorkApi;

namespace Ipm.IntegrationTests.Activity;

/// <summary>The scenarios of docs/modules/11-telegram-notifications.md: linking, the webhook, the outbox and the dispatcher, against the fake bot.</summary>
[Collection(ApiCollection.Name)]
public sealed class TelegramTests(ApiFactory factory)
{
    private static long NewChat() => Random.Shared.NextInt64(1_000_000_000, long.MaxValue / 4);

    private static string TokenIn(string deepLink) => deepLink[(deepLink.IndexOf("start=", StringComparison.Ordinal) + "start=".Length)..];

    private static async Task<JsonElement> StatusAsync(HttpClient client) => (await GetAsync(client, "/api/v1/telegram/link")).Body;

    private static async Task<string> IssueTokenAsync(HttpClient client)
    {
        var response = await client.PostAsync(new Uri("/api/v1/telegram/link", UriKind.Relative), content: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return TokenIn((await ReadAsync(response)).GetProperty("deepLink").GetString()!);
    }

    private Task<HttpResponseMessage> WebhookAsync(
        string text, long chatId, string chatType = "private", string? username = "tg_user", string? secret = ApiFactory.TelegramWebhookSecret, string? raw = null)
    {
        var update = raw ?? JsonSerializer.Serialize(new
        {
            update_id = Random.Shared.NextInt64(1, 1_000_000),
            message = new { message_id = 1, from = new { id = chatId, username }, chat = new { id = chatId, type = chatType }, text },
        });
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/telegram/webhook") { Content = new StringContent(update, Encoding.UTF8, "application/json") };
        if (secret is not null)
        {
            request.Headers.Add("X-Telegram-Bot-Api-Secret-Token", secret);
        }

        return factory.CreateClient().SendAsync(request);
    }

    /// <summary>Signs in, drops any old link and links the user to a new chat the way a person does: deep link, then Start.</summary>
    private async Task<(Guid UserId, long Chat, HttpClient Client)> LinkedAsync(string username, string? telegramName = "tg_user")
    {
        var client = await factory.SignedInClientAsync(username, ApiFactory.DemoPassword);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(new Uri("/api/v1/telegram/link", UriKind.Relative))).StatusCode);
        var chat = NewChat();
        Assert.Equal(HttpStatusCode.OK, (await WebhookAsync($"/start {await IssueTokenAsync(client)}", chat, username: telegramName)).StatusCode);
        Assert.True((await StatusAsync(client)).GetProperty("linked").GetBoolean());
        return (await UserIdAsync(await factory.AdminClientAsync(), username), chat, client);
    }

    private async Task<(Guid ItemId, string Ref)> AssignAsync(Guid userId, string title = "عنصر اختبار")
    {
        using var admin = await factory.AdminClientAsync();
        var (project, _) = await NewProjectAsync(admin);
        var (itemId, _, item) = await CreateAsync(admin, project, title: title, assignees: [userId]);
        return (itemId, item.GetProperty("ref").GetString()!);
    }

    private async Task<T> InDbAsync<T>(Func<ApplicationDbContext, Task<T>> read)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await read(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private Task<List<TelegramMessage>> OutboxAsync(Guid userId) =>
        InDbAsync(db => db.TelegramOutbox.AsNoTracking().Where(m => m.UserId == userId).OrderBy(m => m.CreatedAt).ToListAsync());

    private Task<TelegramLink?> LinkOfAsync(Guid userId) => InDbAsync(db => db.TelegramLinks.AsNoTracking().SingleOrDefaultAsync(l => l.UserId == userId));

    /// <summary>A test starts without the rows of the earlier ones, so a pass of the dispatcher only touches its own and the fake answers go to the right send.</summary>
    private async Task CleanOutboxAsync() =>
        await InDbAsync(async db => await db.Database.ExecuteSqlRawAsync("DELETE FROM activity.telegram_outbox"));

    private Task<int> DispatchAsync() => factory.Services.GetRequiredService<TelegramOutboxDispatcher>().DispatchOnceAsync(CancellationToken.None);

    // ───── linking (R2, R3, R4, R5) ─────

    [Fact]
    public async Task Scenario_1_R2_R3_a_deep_link_and_Start_link_the_account()
    {
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        await reem.DeleteAsync(new Uri("/api/v1/telegram/link", UriKind.Relative));

        var before = await StatusAsync(reem);
        Assert.True(before.GetProperty("enabled").GetBoolean());
        Assert.Equal("ipm_test_bot", before.GetProperty("botUsername").GetString());
        Assert.False(before.GetProperty("linked").GetBoolean());

        var response = await reem.PostAsync(new Uri("/api/v1/telegram/link", UriKind.Relative), content: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var issued = await ReadAsync(response);
        var deepLink = issued.GetProperty("deepLink").GetString()!;
        var token = TokenIn(deepLink);
        Assert.StartsWith("https://t.me/ipm_test_bot?start=", deepLink, StringComparison.Ordinal);
        Assert.InRange(token.Length, 40, 64);
        Assert.Matches("^[A-Za-z0-9_-]+$", token);
        var expires = issued.GetProperty("expiresAt").GetDateTimeOffset();
        Assert.InRange(expires, DateTimeOffset.UtcNow.AddMinutes(9), DateTimeOffset.UtcNow.AddMinutes(11));

        var chat = NewChat();
        Assert.Equal(HttpStatusCode.OK, (await WebhookAsync($"/start {token}", chat, username: "reem_tg")).StatusCode);

        var after = await StatusAsync(reem);
        Assert.True(after.GetProperty("linked").GetBoolean());
        Assert.Equal("reem_tg", after.GetProperty("telegramUsername").GetString());
        Assert.NotEqual(JsonValueKind.Null, after.GetProperty("linkedAt").ValueKind);
        Assert.Contains("تم ربط", Assert.Single(factory.TelegramBot.SentTo(chat)).Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Scenario_2_R2_R3_a_used_replaced_expired_or_wrong_token_links_nothing()
    {
        using var fahad = await factory.SignedInClientAsync("fahad", ApiFactory.DemoPassword);
        await fahad.DeleteAsync(new Uri("/api/v1/telegram/link", UriKind.Relative));
        var fahadId = await UserIdAsync(await factory.AdminClientAsync(), "fahad");

        var first = await IssueTokenAsync(fahad);
        var second = await IssueTokenAsync(fahad);
        var replaced = NewChat();
        await WebhookAsync($"/start {first}", replaced);
        Assert.False((await StatusAsync(fahad)).GetProperty("linked").GetBoolean());
        Assert.Contains("اربط تلغرام", Assert.Single(factory.TelegramBot.SentTo(replaced)).Html, StringComparison.Ordinal);

        var real = NewChat();
        await WebhookAsync($"/start {second}", real);
        Assert.True((await StatusAsync(fahad)).GetProperty("linked").GetBoolean());

        var reuse = NewChat();
        await WebhookAsync($"/start {second}", reuse);
        Assert.Equal(real, (await LinkOfAsync(fahadId))!.ChatId);
        Assert.Contains("اربط تلغرام", Assert.Single(factory.TelegramBot.SentTo(reuse)).Html, StringComparison.Ordinal);

        var expired = await IssueTokenAsync(fahad);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var key = $"telegram:link:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(expired)))}";
            await scope.ServiceProvider.GetRequiredService<ICacheService>().RemoveAsync(key);
        }

        var late = NewChat();
        await WebhookAsync($"/start {expired}", late);
        await WebhookAsync("/start not-a-real-token", late);
        await WebhookAsync("/start", late);
        Assert.Equal(real, (await LinkOfAsync(fahadId))!.ChatId);
        Assert.All(factory.TelegramBot.SentTo(late), m => Assert.Contains("اربط تلغرام", m.Html, StringComparison.Ordinal));
        Assert.Equal(3, factory.TelegramBot.SentTo(late).Count);
    }

    [Fact]
    public async Task Scenario_3_R10_the_webhook_needs_the_secret_and_a_small_body_and_ignores_what_it_does_not_know()
    {
        var chat = NewChat();

        var wrong = await WebhookAsync("/start x", chat, secret: "wrong-secret");
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal("Telegram.SecretInvalid", (await ReadAsync(wrong)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await WebhookAsync("/start x", chat, secret: null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await WebhookAsync("/start x", chat, secret: "")).StatusCode);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await WebhookAsync("x", chat, raw: new string('x', 70 * 1024))).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await WebhookAsync("x", chat, raw: "this is not json")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await WebhookAsync("x", chat, raw: """{"update_id":5,"edited_message":{}}""")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await WebhookAsync("hello there", chat)).StatusCode);
        Assert.Empty(factory.TelegramBot.SentTo(chat));
    }

    [Fact]
    public async Task Scenario_3b_R3_only_a_private_chat_can_link_and_a_group_does_not_use_up_the_token()
    {
        using var noura = await factory.SignedInClientAsync("noura", ApiFactory.DemoPassword);
        await noura.DeleteAsync(new Uri("/api/v1/telegram/link", UriKind.Relative));
        var token = await IssueTokenAsync(noura);
        var group = NewChat();

        Assert.Equal(HttpStatusCode.OK, (await WebhookAsync($"/start {token}", group, chatType: "supergroup")).StatusCode);

        Assert.False((await StatusAsync(noura)).GetProperty("linked").GetBoolean());
        Assert.Empty(factory.TelegramBot.SentTo(group));
        await WebhookAsync($"/start {token}", NewChat());
        Assert.True((await StatusAsync(noura)).GetProperty("linked").GetBoolean());
    }

    [Fact]
    public async Task Scenario_4_R3_R4_a_chat_belongs_to_one_account_and_stop_unlinks()
    {
        var (reemId, chat, reem) = await LinkedAsync("reem");
        using var fahad = await factory.SignedInClientAsync("fahad", ApiFactory.DemoPassword);
        await fahad.DeleteAsync(new Uri("/api/v1/telegram/link", UriKind.Relative));

        await WebhookAsync($"/start {await IssueTokenAsync(fahad)}", chat);

        Assert.True((await StatusAsync(fahad)).GetProperty("linked").GetBoolean());
        Assert.False((await StatusAsync(reem)).GetProperty("linked").GetBoolean());
        Assert.Null(await LinkOfAsync(reemId));

        await WebhookAsync("/stop", chat);
        Assert.False((await StatusAsync(fahad)).GetProperty("linked").GetBoolean());
        Assert.Contains("فك الربط", factory.TelegramBot.SentTo(chat)[^1].Html, StringComparison.Ordinal);

        reem.Dispose();
    }

    [Fact]
    public async Task Scenario_6_R4_unlinking_twice_is_fine_and_a_message_waiting_for_a_gone_link_is_not_sent()
    {
        await CleanOutboxAsync();
        var (userId, chat, client) = await LinkedAsync("reem");
        await AssignAsync(userId);
        Assert.Single(await OutboxAsync(userId));

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(new Uri("/api/v1/telegram/link", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(new Uri("/api/v1/telegram/link", UriKind.Relative))).StatusCode);
        Assert.False((await StatusAsync(client)).GetProperty("linked").GetBoolean());

        await AssignAsync(userId);
        Assert.Single(await OutboxAsync(userId));

        await DispatchAsync();
        var row = Assert.Single(await OutboxAsync(userId));
        Assert.Equal(TelegramMessageStatus.Failed, row.Status);
        Assert.DoesNotContain(factory.TelegramBot.SentTo(chat), m => !m.Html.Contains("تم ربط", StringComparison.Ordinal));

        client.Dispose();
    }

    // ───── the outbox (R6, R7) ─────

    [Fact]
    public async Task Scenario_4_R6_R7_assigning_queues_one_message_for_a_linked_assignee_and_nobody_else()
    {
        await CleanOutboxAsync();
        var (fahadId, fahadChat, fahad) = await LinkedAsync("fahad");
        var (reemId, _, reem) = await LinkedAsync("reem");
        await reem.DeleteAsync(new Uri("/api/v1/telegram/link", UriKind.Relative));
        using var actor = await factory.AdminClientAsync();
        var adminId = await MyIdAsync(actor);
        await actor.DeleteAsync(new Uri("/api/v1/telegram/link", UriKind.Relative));
        await WebhookAsync($"/start {await IssueTokenAsync(actor)}", NewChat());
        Assert.True((await StatusAsync(actor)).GetProperty("linked").GetBoolean());

        var (project, _) = await NewProjectAsync(actor);
        var (itemId, _, item) = await CreateAsync(actor, project, title: "<b>مهم</b> & عاجل", assignees: [fahadId, adminId, reemId]);
        var reference = item.GetProperty("ref").GetString()!;

        var row = Assert.Single(await OutboxAsync(fahadId));
        Assert.Equal(TelegramMessageStatus.Pending, row.Status);
        Assert.Equal(fahadChat, row.ChatId);
        Assert.Contains(reference, row.Text, StringComparison.Ordinal);
        Assert.Contains("&lt;b&gt;مهم&lt;/b&gt; &amp; عاجل", row.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>مهم", row.Text, StringComparison.Ordinal);
        Assert.Equal($"https://ipm.example/work-items/{itemId}", row.Url);
        Assert.EndsWith(row.Url, row.Text, StringComparison.Ordinal);

        Assert.Empty(await OutboxAsync(adminId));
        Assert.Empty(await OutboxAsync(reemId));

        await MoveAsync(actor, itemId, "in_progress");
        Assert.Single(await OutboxAsync(fahadId));

        fahad.Dispose();
        reem.Dispose();
    }

    // ───── the dispatcher (R8, R9) ─────

    [Fact]
    public async Task Scenario_5_R8_the_dispatcher_sends_once_with_the_button_and_marks_the_row_sent()
    {
        await CleanOutboxAsync();
        var (userId, chat, client) = await LinkedAsync("reem");
        var (itemId, reference) = await AssignAsync(userId, "مهمة <i>جديدة</i>");

        Assert.Equal(1, await DispatchAsync());

        var sent = Assert.Single(factory.TelegramBot.SentTo(chat), m => m.Html.Contains(reference, StringComparison.Ordinal));
        Assert.Equal($"https://ipm.example/work-items/{itemId}", sent.ButtonUrl);
        Assert.Contains("&lt;i&gt;جديدة&lt;/i&gt;", sent.Html, StringComparison.Ordinal);
        var row = Assert.Single(await OutboxAsync(userId));
        Assert.Equal(TelegramMessageStatus.Sent, row.Status);
        Assert.NotNull(row.SentAt);
        Assert.Equal(1, row.Attempts);

        Assert.Equal(0, await DispatchAsync());
        Assert.Single(factory.TelegramBot.SentTo(chat), m => m.Html.Contains(reference, StringComparison.Ordinal));

        client.Dispose();
    }

    [Fact]
    public async Task Scenario_5_R9_a_429_waits_for_retry_after_without_counting_as_a_failure()
    {
        await CleanOutboxAsync();
        var (userId, chat, client) = await LinkedAsync("reem");
        await AssignAsync(userId);
        factory.TelegramBot.Answer(TelegramSendResult.After(TimeSpan.FromSeconds(60)));

        await DispatchAsync();

        var row = Assert.Single(await OutboxAsync(userId));
        Assert.Equal(TelegramMessageStatus.Pending, row.Status);
        Assert.Equal(0, row.Attempts);
        Assert.InRange(row.NextAttemptAt, DateTimeOffset.UtcNow.AddSeconds(50), DateTimeOffset.UtcNow.AddSeconds(70));
        var sends = factory.TelegramBot.SentTo(chat).Count;

        await DispatchAsync();
        Assert.Equal(sends, factory.TelegramBot.SentTo(chat).Count);

        client.Dispose();
    }

    [Fact]
    public async Task Scenario_5_R9_a_blocked_bot_fails_the_row_switches_the_link_off_and_a_new_Start_switches_it_on()
    {
        await CleanOutboxAsync();
        var (userId, _, client) = await LinkedAsync("reem");
        await AssignAsync(userId);
        factory.TelegramBot.Answer(TelegramSendResult.Blocked("Telegram 403: Forbidden: bot was blocked by the user"));

        await DispatchAsync();

        var row = Assert.Single(await OutboxAsync(userId));
        Assert.Equal(TelegramMessageStatus.Failed, row.Status);
        Assert.Contains("403", row.LastError, StringComparison.Ordinal);
        Assert.NotNull((await LinkOfAsync(userId))!.DisabledAt);
        Assert.False((await StatusAsync(client)).GetProperty("linked").GetBoolean());

        await AssignAsync(userId);
        Assert.Single(await OutboxAsync(userId));

        await WebhookAsync($"/start {await IssueTokenAsync(client)}", NewChat());
        Assert.True((await StatusAsync(client)).GetProperty("linked").GetBoolean());
        Assert.Null((await LinkOfAsync(userId))!.DisabledAt);

        client.Dispose();
    }

    [Fact]
    public async Task Scenario_5_R9_a_temporary_failure_is_retried_later_with_the_backoff()
    {
        await CleanOutboxAsync();
        var (userId, _, client) = await LinkedAsync("reem");
        await AssignAsync(userId);
        factory.TelegramBot.Answer(TelegramSendResult.Transient("timeout"));

        await DispatchAsync();

        var row = Assert.Single(await OutboxAsync(userId));
        Assert.Equal(TelegramMessageStatus.Pending, row.Status);
        Assert.Equal(1, row.Attempts);
        Assert.Equal("timeout", row.LastError);
        Assert.InRange(row.NextAttemptAt, DateTimeOffset.UtcNow.AddSeconds(2), DateTimeOffset.UtcNow.AddSeconds(10));

        client.Dispose();
    }

    [Fact]
    public async Task Scenario_5_R9_another_refusal_fails_at_once_and_a_row_older_than_a_day_expires_unsent()
    {
        await CleanOutboxAsync();
        var (userId, chat, client) = await LinkedAsync("reem");
        await AssignAsync(userId);
        factory.TelegramBot.Answer(TelegramSendResult.Permanent("Telegram 400: message is too long"));
        await DispatchAsync();
        Assert.Equal(TelegramMessageStatus.Failed, Assert.Single(await OutboxAsync(userId)).Status);

        await CleanOutboxAsync();
        await AssignAsync(userId);
        await InDbAsync(async db => await db.Database.ExecuteSqlRawAsync("UPDATE activity.telegram_outbox SET created_at = now() - interval '25 hours'"));
        var before = factory.TelegramBot.SentTo(chat).Count;

        await DispatchAsync();

        Assert.Equal(TelegramMessageStatus.Expired, Assert.Single(await OutboxAsync(userId)).Status);
        Assert.Equal(before, factory.TelegramBot.SentTo(chat).Count);

        client.Dispose();
    }

    [Fact]
    public async Task Scenario_5_R8_a_burst_to_one_chat_goes_out_one_message_per_second()
    {
        await CleanOutboxAsync();
        var (userId, chat, client) = await LinkedAsync("reem");
        await AssignAsync(userId);
        await AssignAsync(userId);
        Assert.Equal(2, (await OutboxAsync(userId)).Count);
        var before = factory.TelegramBot.SentTo(chat).Count;

        await DispatchAsync();

        Assert.Equal(before + 1, factory.TelegramBot.SentTo(chat).Count);
        Assert.Single(await OutboxAsync(userId), m => m.IsPending);

        await Task.Delay(TimeSpan.FromMilliseconds(1300));
        await DispatchAsync();

        Assert.Equal(before + 2, factory.TelegramBot.SentTo(chat).Count);
        Assert.All(await OutboxAsync(userId), m => Assert.Equal(TelegramMessageStatus.Sent, m.Status));

        client.Dispose();
    }

    // ───── off (R1) ─────

    [Fact]
    public async Task Scenario_7_R1_without_a_token_the_feature_is_off_and_the_system_still_runs()
    {
        await using var off = factory.WithWebHostBuilder(builder => builder.UseSetting("Telegram:BotToken", string.Empty));
        using var client = off.CreateClient();
        var login = await client.PostAsJsonAsync(new Uri("/api/v1/auth/login", UriKind.Relative), new { username = ApiFactory.AdminUsername, password = ApiFactory.AdminPassword });
        login.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<SessionDto>())!.AccessToken);

        var status = await StatusAsync(client);
        Assert.False(status.GetProperty("enabled").GetBoolean());
        Assert.False(status.GetProperty("linked").GetBoolean());
        Assert.Equal(JsonValueKind.Null, status.GetProperty("botUsername").ValueKind);

        var issue = await client.PostAsync(new Uri("/api/v1/telegram/link", UriKind.Relative), content: null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, issue.StatusCode);
        Assert.Equal("Telegram.Disabled", (await ReadAsync(issue)).GetProperty("code").GetString());

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/telegram/webhook") { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        request.Headers.Add("X-Telegram-Bot-Api-Secret-Token", ApiFactory.TelegramWebhookSecret);
        Assert.Equal(HttpStatusCode.Unauthorized, (await off.CreateClient().SendAsync(request)).StatusCode);
    }
}
