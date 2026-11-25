using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Abstractions.Telegram;

namespace Ipm.Application.Features.Activity.Telegram.Commands.ProcessTelegramUpdate;

/// <summary>Spec 11 R3 and R4: one update from the polling loop. It has no user: the link token is what identifies the account.</summary>
public sealed record ProcessTelegramUpdateCommand(TelegramUpdate Update) : ICommand;
