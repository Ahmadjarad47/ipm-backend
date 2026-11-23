using Ipm.Application.Abstractions.Messaging;

namespace Ipm.Application.Features.Git.Repositories.Commands.RotateWebhookSecret;

/// <summary>Replaces the webhook secret of a connected repository and returns the new one once (R3, git.manage).</summary>
public sealed record RotateWebhookSecretCommand(Guid ProjectId, Guid RepositoryId) : ICommand<RotatedSecretResponse>;
