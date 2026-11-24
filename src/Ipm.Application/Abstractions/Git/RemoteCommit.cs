namespace Ipm.Application.Abstractions.Git;

/// <summary>A commit as the Git server reports it.</summary>
public sealed record RemoteCommit(string Sha, string Message, string Author, string Url, DateTimeOffset? CommittedAt = null);
