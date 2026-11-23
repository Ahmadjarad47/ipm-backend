using Ipm.Domain.Git;

namespace Ipm.Application.Features.Git.Repositories.Common;

internal static class RepositoryMapping
{
    public static RepositoryResponse ToResponse(this GitRepository r, string? generatedSecret = null) => new(
        r.Id, r.ProjectId, r.Provider, r.Name, r.Url, r.DefaultBranch, r.IsConnected, r.HasCredential,
        $"/api/v1/webhooks/{r.Provider.ToString().ToLowerInvariant()}/{r.Id}", r.LastSyncAt, r.LastSyncError, generatedSecret);
}
