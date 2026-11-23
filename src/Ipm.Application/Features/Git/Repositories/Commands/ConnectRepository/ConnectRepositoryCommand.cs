using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Git.Repositories.Common;
using Ipm.Domain.Git;

namespace Ipm.Application.Features.Git.Repositories.Commands.ConnectRepository;

/// <summary>Links a Git repository to a project (R1, git.manage). Without <paramref name="WebhookSecret"/> one is generated and returned once.</summary>
public sealed record ConnectRepositoryCommand(Guid ProjectId, GitProvider Provider, string Name, string Url, string DefaultBranch, string AccessToken, string? WebhookSecret)
    : ICommand<RepositoryResponse>;
