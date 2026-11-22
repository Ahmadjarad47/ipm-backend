using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Identity.Auth.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Identity.Auth.Commands.Login;

internal sealed class LoginCommandHandler(
    UserManager<User> users, IApplicationDbContext db, ITokenService tokens, ISecurityEventLog securityLog)
    : ICommandHandler<LoginCommand, SessionResult>
{
    public async ValueTask<Result<SessionResult>> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var username = command.Username.Trim().ToLowerInvariant();

        // R2: a global cap on failures stops guessing spread over many accounts and clients.
        if (securityLog.IsLoginBlockedGlobally)
        {
            await securityLog.LogAsync(SecurityEventType.LockedOut, null, username, "global-limit", cancellationToken: cancellationToken);
            return AuthErrors.LockedOut;
        }

        var normalized = users.NormalizeName(username);
        var user = await db.Users.SingleOrDefaultAsync(u => u.NormalizedUserName == normalized, cancellationToken);

        // R1 and R3: unknown and disabled look exactly like a wrong password. The reason stays in the security events.
        if (user is null || !user.IsActive)
        {
            await securityLog.LogAsync(
                SecurityEventType.LoginFailed, user?.Id, username, user is null ? "unknown-user" : "disabled", cancellationToken: cancellationToken);
            return AuthErrors.InvalidCredentials;
        }

        if (await users.IsLockedOutAsync(user))
        {
            await securityLog.LogAsync(SecurityEventType.LockedOut, user.Id, username, "locked", cancellationToken: cancellationToken);
            return AuthErrors.LockedOut;
        }

        if (!await users.CheckPasswordAsync(user, command.Password))
        {
            await users.AccessFailedAsync(user); // locks the account after 5 failures
            await securityLog.LogAsync(SecurityEventType.LoginFailed, user.Id, username, "wrong-password", cancellationToken: cancellationToken);
            return await users.IsLockedOutAsync(user) ? AuthErrors.LockedOut : AuthErrors.InvalidCredentials;
        }

        if (user.AccessFailedCount > 0)
        {
            await users.ResetAccessFailedCountAsync(user);
        }

        await securityLog.LogAsync(SecurityEventType.LoginSucceeded, user.Id, username, cancellationToken: cancellationToken);
        return new SessionResult(await tokens.IssueAsync(user, cancellationToken), user.MustChangePassword);
    }
}
