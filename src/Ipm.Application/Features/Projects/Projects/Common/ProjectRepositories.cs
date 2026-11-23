using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Features.Git.Repositories.Common;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Projects.Projects.Common;

/// <summary>The Git repositories shown with a project: only to those who hold git.view in it, so the project list needs no second call.</summary>
internal static class ProjectRepositories
{
    public static async Task<Dictionary<Guid, IReadOnlyList<RepositoryResponse>>> LoadAsync(
        IApplicationDbContext db, UserAccess caller, IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken)
    {
        var allowed = projectIds.Where(id => caller.Can(Permissions.GitView, id)).ToList();
        if (allowed.Count == 0)
        {
            return [];
        }

        var rows = await db.GitRepositories.AsNoTracking().Where(r => allowed.Contains(r.ProjectId)).OrderBy(r => r.Name).ToListAsync(cancellationToken);
        return rows.GroupBy(r => r.ProjectId).ToDictionary(g => g.Key, g => (IReadOnlyList<RepositoryResponse>)[.. g.Select(r => r.ToResponse())]);
    }

    public static ProjectResponse With(this ProjectResponse response, Dictionary<Guid, IReadOnlyList<RepositoryResponse>> repositories) =>
        response with { Repositories = repositories.GetValueOrDefault(response.Id) ?? [] };
}
