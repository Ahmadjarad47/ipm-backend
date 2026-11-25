namespace Ipm.Application.Features.Activity.Telegram.Common;

/// <summary>Spec 11 R5. <see cref="Linked"/> is false when there is no link or the bot was blocked.</summary>
public sealed record TelegramLinkResponse(bool Enabled, string? BotUsername, bool Linked, string? TelegramUsername, DateTimeOffset? LinkedAt);
