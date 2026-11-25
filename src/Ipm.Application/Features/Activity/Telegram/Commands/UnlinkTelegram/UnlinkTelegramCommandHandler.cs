using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Activity.Telegram.Commands.UnlinkTelegram;

internal sealed class UnlinkTelegramCommandHandler(IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser) : ICommandHandler<UnlinkTelegramCommand>
{
    public async ValueTask<Result> Handle(UnlinkTelegramCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } me)
        {
            return AuthErrors.Unauthenticated;
        }

        var link = await db.TelegramLinks.SingleOrDefaultAsync(l => l.UserId == me, cancellationToken);
        if (link is not null)
        {
            db.TelegramLinks.Remove(link);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
