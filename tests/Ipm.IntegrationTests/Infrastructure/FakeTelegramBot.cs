using System.Collections.Concurrent;
using Ipm.Application.Abstractions.Telegram;
using Ipm.Domain.Common;

namespace Ipm.IntegrationTests.Infrastructure;

/// <summary>Stands in for the Telegram Bot API: records what was sent, and a test queues what the next sends answer.</summary>
public sealed class FakeTelegramBot : ITelegramBot
{
    private readonly ConcurrentQueue<TelegramSendResult> answers = new();

    public ConcurrentQueue<SentMessage> Sent { get; } = new();

    /// <summary>The answer of the next send. Without one, a send succeeds.</summary>
    public void Answer(TelegramSendResult result) => answers.Enqueue(result);

    public IReadOnlyList<SentMessage> SentTo(long chatId) => [.. Sent.Where(m => m.ChatId == chatId)];

    public Task<TelegramSendResult> SendMessageAsync(long chatId, string html, string? buttonUrl, CancellationToken cancellationToken)
    {
        Sent.Enqueue(new SentMessage(chatId, html, buttonUrl));
        return Task.FromResult(answers.TryDequeue(out var answer) ? answer : TelegramSendResult.Ok);
    }

    public Task<Result<IReadOnlyList<TelegramUpdate>>> GetUpdatesAsync(long offset, int timeoutSeconds, CancellationToken cancellationToken) =>
        Task.FromResult<Result<IReadOnlyList<TelegramUpdate>>>(new List<TelegramUpdate>());

    public Task<Result> SetWebhookAsync(string url, string secretToken, CancellationToken cancellationToken) => Task.FromResult(Result.Success());

    public Task<Result> DeleteWebhookAsync(CancellationToken cancellationToken) => Task.FromResult(Result.Success());
}

public sealed record SentMessage(long ChatId, string Html, string? ButtonUrl);
