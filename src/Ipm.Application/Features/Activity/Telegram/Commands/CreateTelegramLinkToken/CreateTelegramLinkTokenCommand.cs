using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Activity.Telegram.Common;

namespace Ipm.Application.Features.Activity.Telegram.Commands.CreateTelegramLinkToken;

/// <summary>Spec 11 R2: starts linking Telegram for the caller. A new token replaces the previous one.</summary>
public sealed record CreateTelegramLinkTokenCommand : ICommand<TelegramLinkTokenResponse>;
