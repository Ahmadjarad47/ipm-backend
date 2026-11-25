namespace Ipm.Application.Abstractions.Telegram;

/// <summary>A fresh one-time link token (spec 11 R2). <see cref="Token"/> is shown once; only its hash is stored.</summary>
public sealed record TelegramLinkToken(string Token, DateTimeOffset ExpiresAt);
