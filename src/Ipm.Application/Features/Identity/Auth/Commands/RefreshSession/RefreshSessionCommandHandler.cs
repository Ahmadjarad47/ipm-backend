using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Identity.Auth.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Identity.Auth.Commands.RefreshSession;

internal sealed class RefreshSessionCommandHandler(IApplicationDbContext db, ITokenService tokens)
    : ICommandHandler<RefreshSessionCommand, SessionResult>
{
    public async ValueTask<Result<SessionResult>> Handle(RefreshSessionCommand command, CancellationToken cancellationToken)
    {
        var redeemed = await tokens.RedeemAsync(command.RefreshToken, cancellationToken);
        if (redeemed.IsFailure)
        {
            return redeemed.Error;
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == redeemed.Value, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return AuthErrors.RefreshInvalid; // R3
        }

        return new SessionResult(await tokens.IssueAsync(user, cancellationToken), user.MustChangePassword);
    }
}
