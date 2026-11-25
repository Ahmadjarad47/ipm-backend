using System.Collections.Concurrent;
using System.Threading.RateLimiting;
using Ipm.Application.Abstractions.Telegram;
using Ipm.Domain.Activity;
using Ipm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ipm.Infrastructure.Telegram;

/// <summary>
/// Spec 11 R8 and R9: one pass over the outbox. <see cref="DispatchOnceAsync"/> claims the due rows, sends them within Telegram limits and records
/// the outcome of each. The hosted worker calls it every second; integration tests call it by hand.
/// <list type="bullet">
/// <item>Claim: a short transaction selects the due rows <c>FOR UPDATE SKIP LOCKED</c> and moves their next attempt a few minutes ahead (a lease), so
/// no other instance takes them, and a crash during the send only delays them. The send happens outside the transaction.</item>
/// <item>Limits: one token bucket for all chats (<see cref="TelegramOptions.MessagesPerSecond"/>) and at least a second between two messages to the same chat,
/// the second one waits by being rescheduled, not by blocking.</item>
/// </list>
/// </summary>
public sealed partial class TelegramOutboxDispatcher(
    IServiceScopeFactory scopes, IOptions<TelegramOptions> options, TimeProvider time, ILogger<TelegramOutboxDispatcher> logger) : IDisposable
{
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ChatSpacing = TimeSpan.FromSeconds(1);

    private readonly TokenBucketRateLimiter limiter = new(new TokenBucketRateLimiterOptions
    {
        TokenLimit = options.Value.MessagesPerSecond,
        TokensPerPeriod = options.Value.MessagesPerSecond,
        ReplenishmentPeriod = TimeSpan.FromSeconds(1),
        QueueLimit = 0,
        AutoReplenishment = true,
    });

    private readonly ConcurrentDictionary<long, DateTimeOffset> lastSentToChat = new();

    /// <summary>Returns how many messages were sent. Failures of single messages are recorded on their rows and never thrown.</summary>
    public async Task<int> DispatchOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var bot = scope.ServiceProvider.GetRequiredService<ITelegramBot>();

        var batch = await ClaimAsync(db, cancellationToken);
        if (batch.Count == 0)
        {
            return 0;
        }

        var links = await LoadLinksAsync(db, batch, cancellationToken);
        var sent = 0;
        var index = 0;
        for (; index < batch.Count; index++)
        {
            var message = batch[index];
            var now = time.GetUtcNow();

            if (message.IsStale(now))
            {
                message.Expire();
            }
            else if (!links.TryGetValue(message.UserId, out var link) || !link.IsActive)
            {
                message.Fail("ربط تلغرام غير فعّال.");
            }
            else if (WaitForChat(link.ChatId, now) is { } wait)
            {
                message.Defer(now, wait);
            }
            else if (!limiter.AttemptAcquire(1).IsAcquired)
            {
                // The global budget of this second is spent: give the rest back and stop.
                break;
            }
            else
            {
                if (await SendAsync(bot, message, link, now, cancellationToken))
                {
                    sent++;
                }
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        if (index < batch.Count)
        {
            foreach (var untouched in batch.Skip(index))
            {
                untouched.Defer(time.GetUtcNow(), TimeSpan.Zero);
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        Forget(time.GetUtcNow());
        return sent;
    }

    public void Dispose() => limiter.Dispose();

    private async Task<List<TelegramMessage>> ClaimAsync(ApplicationDbContext db, CancellationToken cancellationToken)
    {
        var size = options.Value.MessagesPerSecond;
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var now = time.GetUtcNow();
            var rows = await db.TelegramOutbox
                .FromSqlInterpolated($"""
                    SELECT * FROM activity.telegram_outbox
                    WHERE status = 'pending' AND next_attempt_at <= {now}
                    ORDER BY next_attempt_at, id
                    LIMIT {size}
                    FOR UPDATE SKIP LOCKED
                    """)
                .ToListAsync(cancellationToken);
            foreach (var row in rows)
            {
                row.Defer(now, Lease);
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return rows;
        });
    }

    private static Task<Dictionary<Guid, TelegramLink>> LoadLinksAsync(ApplicationDbContext db, List<TelegramMessage> batch, CancellationToken cancellationToken)
    {
        var users = batch.Select(m => m.UserId).Distinct().ToList();
        return db.TelegramLinks.Where(l => users.Contains(l.UserId)).ToDictionaryAsync(l => l.UserId, cancellationToken);
    }

    /// <summary>How long the chat still has to rest, or null when it may be written to now.</summary>
    private TimeSpan? WaitForChat(long chatId, DateTimeOffset now) =>
        lastSentToChat.TryGetValue(chatId, out var last) && last + ChatSpacing > now ? last + ChatSpacing - now : null;

    private async Task<bool> SendAsync(ITelegramBot bot, TelegramMessage message, TelegramLink link, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var button = TelegramMessageFormatter.CanUseButton(message.Url) ? message.Url : null;
        var result = await bot.SendMessageAsync(link.ChatId, message.Text, button, cancellationToken);
        lastSentToChat[link.ChatId] = now;

        switch (result.Outcome)
        {
            case TelegramSendOutcome.Ok:
                message.MarkSent(time.GetUtcNow());
                return true;
            case TelegramSendOutcome.RetryAfter:
                message.Defer(time.GetUtcNow(), result.RetryAfter ?? TelegramBotClient.DefaultRetryAfter, result.Error);
                LogRateLimited(logger, (int)(result.RetryAfter ?? TelegramBotClient.DefaultRetryAfter).TotalSeconds);
                break;
            case TelegramSendOutcome.Blocked:
                message.Fail(result.Error ?? "Blocked");
                link.Disable(time.GetUtcNow());
                LogBlocked(logger, message.Id);
                break;
            case TelegramSendOutcome.Transient:
                message.Retry(time.GetUtcNow(), result.Error ?? "Transient");
                LogTransient(logger, message.Id, message.Attempts);
                break;
            default:
                message.Fail(result.Error ?? "Permanent");
                LogFailed(logger, message.Id);
                break;
        }

        return false;
    }

    /// <summary>The per-chat map only needs the last second; older entries are dropped so it never grows.</summary>
    private void Forget(DateTimeOffset now)
    {
        foreach (var (chat, last) in lastSentToChat)
        {
            if (now - last > TimeSpan.FromMinutes(1))
            {
                lastSentToChat.TryRemove(chat, out _);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Telegram asked to slow down: next try in {Seconds} s")]
    private static partial void LogRateLimited(ILogger logger, int seconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Telegram message {MessageId} was refused because the bot is blocked; the link is switched off")]
    private static partial void LogBlocked(ILogger logger, Guid messageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Telegram message {MessageId} failed for now (attempt {Attempts}); it will be retried")]
    private static partial void LogTransient(ILogger logger, Guid messageId, int attempts);

    [LoggerMessage(Level = LogLevel.Error, Message = "Telegram message {MessageId} was refused for good")]
    private static partial void LogFailed(ILogger logger, Guid messageId);
}
