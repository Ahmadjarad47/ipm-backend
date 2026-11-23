using Ipm.Domain.Git;

namespace Ipm.Api.Endpoints.Git.Repositories.ConnectRepository;

public sealed record ConnectRepositoryRequest(GitProvider Provider, string Name, string Url, string DefaultBranch, string AccessToken, string? WebhookSecret);
