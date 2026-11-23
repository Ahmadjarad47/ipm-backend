using Ipm.Domain.Git;

namespace Ipm.Application.Features.Git.Webhooks.Common;

/// <summary>One webhook call in the log. The raw body is never returned.</summary>
public sealed record DeliveryResponse(
    Guid Id,
    Guid ProjectId,
    Guid RepositoryId,
    GitProvider Provider,
    string DeliveryId,
    string Event,
    bool SignatureValid,
    DeliveryOutcome Outcome,
    string? Message,
    IReadOnlyList<string> LinkedRefs,
    DateTimeOffset CreatedAt);
