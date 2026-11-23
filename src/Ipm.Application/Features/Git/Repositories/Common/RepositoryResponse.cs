using Ipm.Domain.Git;

namespace Ipm.Application.Features.Git.Repositories.Common;

/// <summary>
/// A connected (or disconnected) repository. Neither the access token nor the webhook secret is ever part of it (R15), except
/// <see cref="WebhookSecret"/> once, in the answer to connecting with a generated secret.
/// <see cref="WebhookUrl"/> is the path the Git server must call; the client puts its own host in front.
/// </summary>
public sealed record RepositoryResponse(
    Guid Id,
    Guid ProjectId,
    GitProvider Provider,
    string Name,
    string Url,
    string DefaultBranch,
    bool Connected,
    bool HasCredential,
    string WebhookUrl,
    DateTimeOffset? LastSyncAt,
    string? LastSyncError,
    string? WebhookSecret = null);
