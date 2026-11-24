using Ipm.Domain.Git;

namespace Ipm.Application.Features.Git.GitEvents.Common;

/// <summary>A commit or a pull request with the work items it is linked to. <see cref="Version"/> is sent as the ETag.
/// <see cref="OccurredAt"/> is when it happened on the Git server (commit or pull request time), or when it was received if the server did not say.</summary>
public sealed record GitEventResponse(
    Guid Id,
    Guid ProjectId,
    Guid RepositoryId,
    GitEventKind Kind,
    string Title,
    string Branch,
    string Sha,
    string Author,
    string Url,
    IReadOnlyList<string> WorkItemRefs,
    IReadOnlyList<string> ManualRefs,
    int? PrNumber,
    PullRequestState? PrState,
    string? DeliveryId,
    DateTimeOffset CreatedAt,
    uint Version,
    DateTimeOffset OccurredAt);
