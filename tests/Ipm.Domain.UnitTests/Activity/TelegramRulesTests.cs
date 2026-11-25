using Ipm.Domain.Activity;

namespace Ipm.Domain.UnitTests.Activity;

/// <summary>R3, R4, R7 and R9 of docs/modules/11-telegram-notifications.md at the domain level.</summary>
public sealed class TelegramRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 9, 0, 0, TimeSpan.Zero);

    private static TelegramMessage NewMessage() => TelegramMessage.Create(Guid.NewGuid(), Guid.NewGuid(), 42, "hi", null, Now);

    [Fact]
    public void A_new_link_is_active_and_cleans_the_username()
    {
        var link = TelegramLink.Create(Guid.NewGuid(), 7, "  ali\u0007  ", Now);

        Assert.True(link.IsActive);
        Assert.Equal("ali", link.TelegramUsername);
        Assert.Equal(Now, link.LinkedAt);
        Assert.Null(TelegramLink.Create(Guid.NewGuid(), 7, "   ", Now).TelegramUsername);
    }

    [Fact]
    public void Disabling_keeps_the_first_time_and_relinking_switches_it_on_again_with_the_new_chat()
    {
        var link = TelegramLink.Create(Guid.NewGuid(), 7, "ali", Now);
        link.Disable(Now.AddHours(1));
        link.Disable(Now.AddHours(2));

        Assert.False(link.IsActive);
        Assert.Equal(Now.AddHours(1), link.DisabledAt);

        link.Relink(9, null, Now.AddDays(1));

        Assert.True(link.IsActive);
        Assert.Equal(9, link.ChatId);
        Assert.Null(link.TelegramUsername);
        Assert.Equal(Now.AddDays(1), link.LinkedAt);
    }

    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    [InlineData(3, 20)]
    [InlineData(7, 320)]
    [InlineData(8, 600)]
    [InlineData(50, 600)]
    public void The_backoff_doubles_from_5_seconds_and_stops_at_10_minutes(int failures, int seconds) =>
        Assert.Equal(TimeSpan.FromSeconds(seconds), TelegramMessage.Backoff(failures));

    [Fact]
    public void A_failed_attempt_is_retried_later_and_the_eighth_is_final()
    {
        var message = NewMessage();

        message.Retry(Now, "timeout");
        Assert.Equal(TelegramMessageStatus.Pending, message.Status);
        Assert.Equal(1, message.Attempts);
        Assert.Equal(Now.AddSeconds(5), message.NextAttemptAt);
        Assert.Equal("timeout", message.LastError);

        for (var i = 0; i < TelegramMessage.MaxAttempts - 2; i++)
        {
            message.Retry(Now, "timeout");
        }

        Assert.Equal(TelegramMessageStatus.Pending, message.Status);
        message.Retry(Now, "timeout");

        Assert.Equal(TelegramMessageStatus.Failed, message.Status);
        Assert.Equal(TelegramMessage.MaxAttempts, message.Attempts);
    }

    [Fact]
    public void Telegram_retry_after_wins_over_the_backoff_and_a_defer_is_not_an_attempt()
    {
        var message = NewMessage();

        message.Retry(Now, "429", TimeSpan.FromSeconds(30));
        Assert.Equal(Now.AddSeconds(30), message.NextAttemptAt);

        message.Defer(Now, TimeSpan.FromSeconds(1));
        Assert.Equal(1, message.Attempts);
        Assert.Equal(Now.AddSeconds(1), message.NextAttemptAt);
    }

    [Fact]
    public void Sending_fail_and_expire_end_the_row_and_only_a_pending_row_can_expire()
    {
        var sent = NewMessage();
        sent.MarkSent(Now);
        Assert.Equal(TelegramMessageStatus.Sent, sent.Status);
        Assert.Equal(Now, sent.SentAt);
        sent.Expire();
        Assert.Equal(TelegramMessageStatus.Sent, sent.Status);

        var failed = NewMessage();
        failed.Fail("403");
        Assert.Equal(TelegramMessageStatus.Failed, failed.Status);

        var stale = NewMessage();
        Assert.False(stale.IsStale(Now.AddHours(23)));
        Assert.True(stale.IsStale(Now.AddHours(25)));
        stale.Expire();
        Assert.Equal(TelegramMessageStatus.Expired, stale.Status);
        Assert.False(stale.IsStale(Now.AddDays(5)));
    }

    [Fact]
    public void The_text_and_the_error_are_capped()
    {
        var message = TelegramMessage.Create(Guid.NewGuid(), Guid.NewGuid(), 1, new string('x', 5000), null, Now);
        Assert.Equal(TelegramMessage.TextMaxLength, message.Text.Length);

        message.Fail(new string('e', 900));
        Assert.Equal(TelegramMessage.ErrorMaxLength, message.LastError!.Length);
    }

    [Fact]
    public void The_formatter_escapes_every_user_text_and_adds_the_link_line()
    {
        var url = TelegramMessageFormatter.AbsoluteUrl("https://ipm.example/", "/work-items/1");
        var text = TelegramMessageFormatter.Format("<b>x</b> & y", "ERP-1 <script>alert(1)</script>", url);

        Assert.Equal("https://ipm.example/work-items/1", url);
        Assert.StartsWith("<b>&lt;b&gt;x&lt;/b&gt; &amp; y</b>\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", text, StringComparison.Ordinal);
        Assert.EndsWith("\nhttps://ipm.example/work-items/1", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://ipm.example/x", true)]
    [InlineData("http://localhost:8000/x", false)]
    [InlineData(null, false)]
    public void An_inline_button_is_only_for_https(string? url, bool allowed) =>
        Assert.Equal(allowed, TelegramMessageFormatter.CanUseButton(url));

    [Fact]
    public void Without_a_base_address_there_is_no_link()
    {
        Assert.Null(TelegramMessageFormatter.AbsoluteUrl(null, "/x"));
        Assert.Equal("<b>t</b>\nb", TelegramMessageFormatter.Format("t", "b", null));
    }
}
