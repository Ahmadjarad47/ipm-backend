using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Abstractions.Telegram;
using Ipm.Application.Features.Activity.Telegram.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Activity.Telegram.Queries.GetMyTelegramLink;

internal sealed class GetMyTelegramLinkQueryHandler(IApplicationDbContext db, IUserContext currentUser, ITelegramSettings settings)
    : IQueryHandler<GetMyTelegramLinkQuery, TelegramLinkResponse>
{
    public async ValueTask<Result<TelegramLinkResponse>> Handle(GetMyTelegramLinkQuery query, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } me)
        {
            return AuthErrors.Unauthenticated;
        }

        if (!settings.Enabled)
        {
            return new TelegramLinkResponse(false, null, false, null, null);
        }

        var link = await db.TelegramLinks.AsNoTracking().SingleOrDefaultAsync(l => l.UserId == me && l.DisabledAt == null, cancellationToken);
        return new TelegramLinkResponse(true, settings.BotUsername, link is not null, link?.TelegramUsername, link?.LinkedAt);
    }
}
