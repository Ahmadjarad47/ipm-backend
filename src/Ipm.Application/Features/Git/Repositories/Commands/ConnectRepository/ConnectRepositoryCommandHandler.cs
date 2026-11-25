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

namespace Ipm.Application.Features.Git.Repositories.Commands.ConnectRepository;

internal sealed class ConnectRepositoryCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, ISecretProtector protector, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog)
    : ICommandHandler<ConnectRepositoryCommand, RepositoryResponse>
{
    public async ValueTask<Result<RepositoryResponse>> Handle(ConnectRepositoryCommand command, CancellationToken cancellationToken)
    {
        var guarded = await ProjectGuard.ChangeAsync(db, currentUser, access, securityLog, command.ProjectId, Permissions.GitManage, cancellationToken);
        if (guarded.IsFailure)
        {
            return guarded.Error;
        }

        if (guarded.Value.Project.IsArchived)
        {
            return GitErrors.ProjectArchived; // R14
        }

        var name = command.Name.Trim();
        var existing = await db.GitRepositories
            .Where(r => r.ProjectId == command.ProjectId && r.Name == name)
            .OrderByDescending(r => r.IsConnected)
            .ThenByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is { IsConnected: true })
        {
            return GitErrors.AlreadyConnected;
        }

        var generated = command.WebhookSecret is null ? RepositorySecrets.Generate() : null;
        var secret = command.WebhookSecret ?? generated!;
        GitRepository repository;
        if (existing is not null)
        {
            // R2: the same repository connected again picks up its disconnected record and its history.
            var reconnected = existing.Reconnect(command.Provider, command.Url, command.DefaultBranch, protector.Protect(command.AccessToken), protector.Protect(secret));
            if (reconnected.IsFailure)
            {
                return reconnected.Error;
            }

            repository = existing;
        }
        else
        {
            repository = GitRepository.Connect(
                command.ProjectId, command.Provider, name, command.Url, command.DefaultBranch, protector.Protect(command.AccessToken), protector.Protect(secret));
            db.GitRepositories.Add(repository);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return repository.ToResponse(generated);
    }
}
