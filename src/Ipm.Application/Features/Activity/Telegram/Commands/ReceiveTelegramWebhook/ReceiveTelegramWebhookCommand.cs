using Ipm.Application.Abstractions.Messaging;

namespace Ipm.Application.Features.Activity.Telegram.Commands.ReceiveTelegramWebhook;

/// <summary>Spec 11 R10: a call from Telegram. No user: the secret header is the credential. <paramref name="Secret"/> is the header value, if any.</summary>
public sealed record ReceiveTelegramWebhookCommand(string? Secret, string Raw) : ICommand;
