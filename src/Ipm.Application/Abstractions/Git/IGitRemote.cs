using Ipm.Domain.Common;
using Ipm.Domain.Git;

namespace Ipm.Application.Abstractions.Git;

/// <summary>Calls the API of a Git server to read its latest commits (R10 of part 07). Tests replace it with a fake.</summary>
public interface IGitRemote
{
    /// <summary>The latest commits of <paramref name="branch"/> (newest first, up to <paramref name="limit"/>), or the reason the server could not be reached.</summary>
    Task<Result<IReadOnlyList<RemoteCommit>>> RecentCommitsAsync(
        GitProvider provider, string repositoryUrl, string name, string branch, string accessToken, int limit, CancellationToken cancellationToken);
}
