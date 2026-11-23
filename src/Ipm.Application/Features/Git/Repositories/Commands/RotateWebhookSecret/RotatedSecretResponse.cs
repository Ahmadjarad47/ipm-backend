namespace Ipm.Application.Features.Git.Repositories.Commands.RotateWebhookSecret;

/// <summary>The new secret, shown this once.</summary>
public sealed record RotatedSecretResponse(Guid RepositoryId, string Secret);
