using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Telegram;
using Ipm.Domain.Activity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Activity.Telegram.Common;

/// <summary>
/// Spec 11 R3 and R4: what the bot does with one message. Shared by the polling loop and the webhook. Only private chats count; anything it
/// does not understand is ignored. A failed reply never undoes the link (the user can see it in the app).
/// </summary>
internal sealed class TelegramUpdateProcessor(
    IApplicationDbContext db, IUnitOfWork unitOfWork, ITelegramLinkTokens tokens, ITelegramBot bot, TimeProvider time)
{
    public async Task ProcessAsync(TelegramUpdate update, CancellationToken cancellationToken)
    {
        if (update is not { ChatId: { } chatId, ChatType: "private", Text: { } text })
        {
            return;
        }

        var (command, argument) = Split(text);
        switch (command)
        {
            case "/start":
                await StartAsync(chatId, update.Username, argument, cancellationToken);
                break;
            case "/stop":
                await StopAsync(chatId, cancellationToken);
                break;
            default:
                break;
        }
    }

    /// <summary><c>/start abc</c> and <c>/start@my_bot abc</c> both give the command <c>/start</c> and the argument <c>abc</c>.</summary>
    private static (string Command, string Argument) Split(string text)
    {
        var parts = text.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || !parts[0].StartsWith('/'))
        {
            return (string.Empty, string.Empty);
        }

        var command = parts[0].Split('@', 2)[0].ToLowerInvariant();
        return (command, parts.Length > 1 ? parts[1] : string.Empty);
    }

    private async Task StartAsync(long chatId, string? username, string token, CancellationToken cancellationToken)
    {
        var userId = token.Length == 0 ? null : await tokens.ConsumeAsync(token, cancellationToken);
        if (userId is not { } user || !await db.Users.AnyAsync(u => u.Id == user && u.IsActive, cancellationToken))
        {
            await bot.SendMessageAsync(chatId, TelegramReplies.Instructions, null, cancellationToken);
            return;
        }

        // A chat belongs to one account: linking it to this user moves it away from whoever had it.
        var others = await db.TelegramLinks.Where(l => l.ChatId == chatId && l.UserId != user).ToListAsync(cancellationToken);
        db.TelegramLinks.RemoveRange(others);

        var now = time.GetUtcNow();
        var link = await db.TelegramLinks.SingleOrDefaultAsync(l => l.UserId == user, cancellationToken);
        if (link is null)
        {
            db.TelegramLinks.Add(TelegramLink.Create(user, chatId, username, now));
        }
        else
        {
            link.Relink(chatId, username, now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await bot.SendMessageAsync(chatId, TelegramReplies.Linked, null, cancellationToken);
    }

    private async Task StopAsync(long chatId, CancellationToken cancellationToken)
    {
        var links = await db.TelegramLinks.Where(l => l.ChatId == chatId).ToListAsync(cancellationToken);
        if (links.Count > 0)
        {
            db.TelegramLinks.RemoveRange(links);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        await bot.SendMessageAsync(chatId, TelegramReplies.Stopped, null, cancellationToken);
    }
}
