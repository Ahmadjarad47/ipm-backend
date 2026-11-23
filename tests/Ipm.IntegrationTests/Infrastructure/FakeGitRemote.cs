using Ipm.Application.Abstractions.Git;
using Ipm.Domain.Common;
using Ipm.Domain.Git;

namespace Ipm.IntegrationTests.Infrastructure;

/// <summary>Stands in for the Git servers: a test sets what the next sync receives.</summary>
public sealed class FakeGitRemote : IGitRemote
{
    public Result<IReadOnlyList<RemoteCommit>> Next { get; set; } = new List<RemoteCommit>();

    public string? LastToken { get; private set; }

    public Task<Result<IReadOnlyList<RemoteCommit>>> RecentCommitsAsync(
        GitProvider provider, string repositoryUrl, string name, string branch, string accessToken, int limit, CancellationToken cancellationToken)
    {
        LastToken = accessToken;
        return Task.FromResult(Next);
    }
}
