using Ipm.Domain.Git;

namespace Ipm.Application.Features.Git.Webhooks.Common;

/// <summary>What became of a webhook call (R5 to R7).</summary>
public sealed record WebhookResult(DeliveryOutcome Outcome, string? Message, IReadOnlyList<string> LinkedRefs);
