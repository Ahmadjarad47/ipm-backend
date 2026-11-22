using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Domain.Common;

namespace Ipm.Application.Features.Identity.Auth.Commands.Logout;

internal sealed class LogoutCommandHandler(ITokenService tokens) : ICommandHandler<LogoutCommand>
{
    public async ValueTask<Result> Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(command.RefreshToken))
        {
            await tokens.RevokeAsync(command.RefreshToken, cancellationToken);
        }

        return Result.Success();
    }
}
