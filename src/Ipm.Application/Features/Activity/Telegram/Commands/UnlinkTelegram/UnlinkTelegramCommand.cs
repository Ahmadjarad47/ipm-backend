using Ipm.Application.Abstractions.Messaging;

namespace Ipm.Application.Features.Activity.Telegram.Commands.UnlinkTelegram;

/// <summary>Spec 11 R4: removes the caller Telegram link. Without one it does nothing and still succeeds.</summary>
public sealed record UnlinkTelegramCommand : ICommand;
