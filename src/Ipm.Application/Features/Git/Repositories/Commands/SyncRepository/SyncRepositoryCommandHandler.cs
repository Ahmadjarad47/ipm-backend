using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Git;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Abstractions.Security;
using Ipm.Application.Features.Git.Repositories.Common;
using Ipm.Application.Features.Git.Webhooks.Common;
using Ipm.Application.Features.Projects.Projects.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Git;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Git.Repositories.Commands.SyncRepository;

internal sealed class SyncRepositoryCommandHandler(
    IApplicationDbContext db, IUnitOfWork unitOfWork, ISecretProtector protector, IGitRemote remote, IUserContext currentUser, IAccessService access,
    ISecurityEventLog securityLog, TimeProvider time)
    : ICommandHandler<SyncRepositoryCommand, SyncResponse>
{
    private const int Limit = 50;

    public async ValueTask<Result<SyncResponse>> Handle(SyncRepositoryCommand command, CancellationToken cancellationToken)
    {
        var guarded = await ProjectGuard.ChangeAsync(db, currentUser, access, securityLog, command.ProjectId, Permissions.GitSync, cancellationToken);
        if (guarded.IsFailure)
        {
            return guarded.Error;
        }

        var repository = await db.GitRepositories.SingleOrDefaultAsync(r => r.Id == command.RepositoryId && r.ProjectId == command.ProjectId, cancellationToken);
        if (repository is null)
        {
            return GitErrors.RepositoryNotFound;
        }

        if (!repository.IsConnected || repository.AccessTokenCipher is null)
        {
            repository.RecordSyncError("المستودع مفصول؛ لا توجد بيانات اعتماد للمزامنة.");
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return GitErrors.Disconnected;
        }

        var fetched = await remote.RecentCommitsAsync(
            repository.Provider, repository.Url, repository.Name, repository.DefaultBranch, protector.Unprotect(repository.AccessTokenCipher), Limit, cancellationToken);
        if (fetched.IsFailure)
        {
            repository.RecordSyncError(fetched.Error.Description);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return GitErrors.SyncFailed(fetched.Error.Description);
        }

        var project = guarded.Value.Project;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var linked = new List<string>();
        var ingested = 0;
        foreach (var commit in fetched.Value)
        {
            var (added, refs) = await GitIngestion.IngestCommitAsync(
                db, repository, project, repository.DefaultBranch, commit.Sha, commit.Message, commit.Author, commit.Url, $"sync-{repository.Id}-{commit.Sha[..Math.Min(7, commit.Sha.Length)]}", seen, cancellationToken);
            if (added)
            {
                ingested++;
                linked.AddRange(refs.Where(r => !linked.Contains(r)));
            }
        }

        repository.RecordSync(time.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new SyncResponse(fetched.Value.Count, ingested, linked, repository.ToResponse());
    }
}
