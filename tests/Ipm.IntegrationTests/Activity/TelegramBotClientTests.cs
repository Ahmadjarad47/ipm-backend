using System.Net;
using System.Text;
using Ipm.Application.Abstractions.Telegram;
using Ipm.Infrastructure.Telegram;

namespace Ipm.IntegrationTests.Activity;

/// <summary>R9 and R11 of docs/modules/11-telegram-notifications.md: how the real client sorts Telegram answers, and that the token never leaves it.</summary>
public sealed class TelegramBotClientTests
{
    private const string Token = "999:SECRET-TOKEN-VALUE";

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public string? LastBody { get; private set; }

        public Uri? LastUri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUri = request.RequestUri;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return await respond(request);
        }
    }

    private static (TelegramBotClient Bot, StubHandler Handler) Client(HttpStatusCode status, string json = "{}") =>
        Client(_ => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") }));

    private static (TelegramBotClient Bot, StubHandler Handler) Client(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond)
    {
        var handler = new StubHandler(respond);
        return (new TelegramBotClient(new HttpClient(handler) { BaseAddress = new Uri($"https://api.telegram.org/bot{Token}/") }), handler);
    }

    [Fact]
    public async Task A_good_send_is_ok_and_asks_for_html_without_a_link_preview()
    {
        var (bot, handler) = Client(HttpStatusCode.OK, """{"ok":true,"result":{}}""");

        var result = await bot.SendMessageAsync(42, "<b>x</b>", null, CancellationToken.None);

        Assert.Equal(TelegramSendOutcome.Ok, result.Outcome);
        Assert.EndsWith("/sendMessage", handler.LastUri!.AbsolutePath, StringComparison.Ordinal);
        Assert.Contains("\"parse_mode\":\"HTML\"", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("\"disable_web_page_preview\":true", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("\"chat_id\":42", handler.LastBody, StringComparison.Ordinal);
        Assert.DoesNotContain("reply_markup", handler.LastBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_button_is_sent_only_when_given()
    {
        var (bot, handler) = Client(HttpStatusCode.OK);

        await bot.SendMessageAsync(42, "x", "https://ipm.example/work-items/1", CancellationToken.None);

        Assert.Contains("inline_keyboard", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("https://ipm.example/work-items/1", handler.LastBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Too_many_requests_carries_the_retry_after_of_telegram()
    {
        var (bot, _) = Client(HttpStatusCode.TooManyRequests, """{"ok":false,"error_code":429,"description":"Too Many Requests: retry after 17","parameters":{"retry_after":17}}""");

        var result = await bot.SendMessageAsync(42, "x", null, CancellationToken.None);

        Assert.Equal(TelegramSendOutcome.RetryAfter, result.Outcome);
        Assert.Equal(TimeSpan.FromSeconds(17), result.RetryAfter);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, """{"ok":false,"error_code":403,"description":"Forbidden: bot was blocked by the user"}""")]
    [InlineData(HttpStatusCode.BadRequest, """{"ok":false,"error_code":400,"description":"Bad Request: chat not found"}""")]
    public async Task A_blocked_bot_or_a_missing_chat_is_blocked(HttpStatusCode status, string json)
    {
        var (bot, _) = Client(status, json);

        Assert.Equal(TelegramSendOutcome.Blocked, (await bot.SendMessageAsync(42, "x", null, CancellationToken.None)).Outcome);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, TelegramSendOutcome.Transient)]
    [InlineData(HttpStatusCode.BadGateway, TelegramSendOutcome.Transient)]
    [InlineData(HttpStatusCode.BadRequest, TelegramSendOutcome.Permanent)]
    [InlineData(HttpStatusCode.Unauthorized, TelegramSendOutcome.Permanent)]
    public async Task Server_errors_are_tried_again_and_other_refusals_are_not(HttpStatusCode status, TelegramSendOutcome expected)
    {
        var (bot, _) = Client(status, """{"ok":false,"description":"Bad Request: message is too long"}""");

        Assert.Equal(expected, (await bot.SendMessageAsync(42, "x", null, CancellationToken.None)).Outcome);
    }

    [Fact]
    public async Task A_network_failure_is_transient_and_the_token_is_not_in_the_text()
    {
        var (bot, _) = Client(_ => throw new HttpRequestException($"failed: https://api.telegram.org/bot{Token}/sendMessage"));

        var result = await bot.SendMessageAsync(42, "x", null, CancellationToken.None);

        Assert.Equal(TelegramSendOutcome.Transient, result.Outcome);
        Assert.DoesNotContain("SECRET-TOKEN-VALUE", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_timeout_is_transient_but_a_shutdown_still_stops_the_caller()
    {
        var (bot, _) = Client(_ => throw new TaskCanceledException("timed out"));
        Assert.Equal(TelegramSendOutcome.Transient, (await bot.SendMessageAsync(42, "x", null, CancellationToken.None)).Outcome);

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => bot.SendMessageAsync(42, "x", null, cancelled.Token));
    }

    [Fact]
    public async Task Updates_are_read_with_the_offset_and_only_messages_and_failures_name_no_token()
    {
        var (bot, handler) = Client(HttpStatusCode.OK, """{"ok":true,"result":[{"update_id":7,"message":{"chat":{"id":5,"type":"private"},"from":{"username":"ali"},"text":"/start abc"}},{"update_id":8,"edited_message":{}}]}""");

        var result = await bot.GetUpdatesAsync(7, 25, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([7L, 8L], result.Value.Select(u => u.UpdateId));
        Assert.Equal(new TelegramUpdate(7, 5, "private", "/start abc", "ali"), result.Value[0]);
        Assert.Null(result.Value[1].ChatId);
        Assert.Contains("\"offset\":7", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("\"allowed_updates\":[\"message\"]", handler.LastBody, StringComparison.Ordinal);

        var (failing, _) = Client(_ => throw new HttpRequestException($"https://api.telegram.org/bot{Token}/getUpdates"));
        var failed = await failing.GetUpdatesAsync(0, 25, CancellationToken.None);
        Assert.True(failed.IsFailure);
        Assert.DoesNotContain("SECRET-TOKEN-VALUE", failed.Error.Description, StringComparison.Ordinal);
    }
}
