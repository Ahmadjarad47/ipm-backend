using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Git.Repositories.Common;
using Ipm.Application.Features.Projects.Projects.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Git.Repositories.Queries.GetRepositories;

internal sealed class GetRepositoriesQueryHandler(IApplicationDbContext db, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog)
    : IQueryHandler<GetRepositoriesQuery, IReadOnlyList<RepositoryResponse>>
{
    public async ValueTask<Result<IReadOnlyList<RepositoryResponse>>> Handle(GetRepositoriesQuery query, CancellationToken cancellationToken)
    {
        var guarded = await ProjectGuard.ReadWithAsync(db, currentUser, access, securityLog, query.ProjectId, Permissions.GitView, cancellationToken);
        if (guarded.IsFailure)
        {
            return guarded.Error;
        }

        var rows = await db.GitRepositories.AsNoTracking().Where(r => r.ProjectId == query.ProjectId).OrderBy(r => r.Name).ThenBy(r => r.Id).ToListAsync(cancellationToken);
        return rows.Select(r => r.ToResponse()).ToList();
    }
}
