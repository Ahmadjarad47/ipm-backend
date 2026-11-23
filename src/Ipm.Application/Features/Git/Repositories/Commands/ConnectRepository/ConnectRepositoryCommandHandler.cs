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
        if (await db.GitRepositories.AnyAsync(r => r.ProjectId == command.ProjectId && r.IsConnected && r.Name == name, cancellationToken))
        {
            return GitErrors.AlreadyConnected;
        }

        var generated = command.WebhookSecret is null ? RepositorySecrets.Generate() : null;
        var secret = command.WebhookSecret ?? generated!;
        var repository = GitRepository.Connect(
            command.ProjectId, command.Provider, name, command.Url, command.DefaultBranch, protector.Protect(command.AccessToken), protector.Protect(secret));
        db.GitRepositories.Add(repository);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return repository.ToResponse(generated);
    }
}
