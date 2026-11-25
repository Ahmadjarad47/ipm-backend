namespace Ipm.Application.Features.Activity.Telegram.Common;

/// <summary>Spec 11 R2: the link to open in Telegram, and when it stops working.</summary>
public sealed record TelegramLinkTokenResponse(string DeepLink, DateTimeOffset ExpiresAt);
