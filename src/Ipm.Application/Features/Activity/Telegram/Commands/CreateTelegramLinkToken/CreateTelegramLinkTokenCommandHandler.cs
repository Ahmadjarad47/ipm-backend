using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Abstractions.Telegram;
using Ipm.Application.Features.Activity.Telegram.Common;
using Ipm.Domain.Activity;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;

namespace Ipm.Application.Features.Activity.Telegram.Commands.CreateTelegramLinkToken;

internal sealed class CreateTelegramLinkTokenCommandHandler(IUserContext currentUser, ITelegramSettings settings, ITelegramLinkTokens tokens)
    : ICommandHandler<CreateTelegramLinkTokenCommand, TelegramLinkTokenResponse>
{
    public async ValueTask<Result<TelegramLinkTokenResponse>> Handle(CreateTelegramLinkTokenCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } me)
        {
            return AuthErrors.Unauthenticated;
        }

        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.BotUsername))
        {
            return TelegramErrors.Disabled;
        }

        var issued = await tokens.IssueAsync(me, cancellationToken);
        return new TelegramLinkTokenResponse($"https://t.me/{settings.BotUsername}?start={issued.Token}", issued.ExpiresAt);
    }
}
