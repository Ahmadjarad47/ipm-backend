using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Git.Repositories.Common;
using Ipm.Application.Features.Projects.Projects.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Git;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Git.Repositories.Commands.DisconnectRepository;

internal sealed class DisconnectRepositoryCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog)
    : ICommandHandler<DisconnectRepositoryCommand, RepositoryResponse>
{
    public async ValueTask<Result<RepositoryResponse>> Handle(DisconnectRepositoryCommand command, CancellationToken cancellationToken)
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

        repository.Disconnect();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return repository.ToResponse();
    }
}
