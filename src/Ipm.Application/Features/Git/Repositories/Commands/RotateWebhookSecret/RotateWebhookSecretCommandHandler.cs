using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Abstractions.Security;
using Ipm.Application.Features.Git.Repositories.Common;
using Ipm.Application.Features.Projects.Projects.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Git;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Git.Repositories.Commands.RotateWebhookSecret;

internal sealed class RotateWebhookSecretCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, ISecretProtector protector, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog)
    : ICommandHandler<RotateWebhookSecretCommand, RotatedSecretResponse>
{
    public async ValueTask<Result<RotatedSecretResponse>> Handle(RotateWebhookSecretCommand command, CancellationToken cancellationToken)
    {
        var guarded = await ProjectGuard.ChangeAsync(db, currentUser, access, securityLog, command.ProjectId, Permissions.GitManage, cancellationToken);
        if (guarded.IsFailure)
        {
            return guarded.Error;
        }

        var repository = await db.GitRepositories.SingleOrDefaultAsync(r => r.Id == command.RepositoryId && r.ProjectId == command.ProjectId, cancellationToken);
        if (repository is null)
        {
            return GitErrors.RepositoryNotFound;
        }

        var secret = RepositorySecrets.Generate();
        var rotated = repository.RotateSecret(protector.Protect(secret));
        if (rotated.IsFailure)
        {
            return rotated.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new RotatedSecretResponse(repository.Id, secret);
    }
}
