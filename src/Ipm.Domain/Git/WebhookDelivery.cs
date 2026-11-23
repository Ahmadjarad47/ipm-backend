namespace Ipm.Domain.Git;

/// <summary>
/// One webhook call received (FR-GIT-04, FR-GIT-05), kept for the audit, for idempotency and for redelivery. Only a failed one keeps the raw
/// body, so it can be processed again (R9). Not an <c>Entity</c>: it has no ETag, and the audit log is not written for every call.
/// </summary>
public sealed class WebhookDelivery
{
    public const int MessageMaxLength = 500;

    private WebhookDelivery() { }

    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid ProjectId { get; private init; }

    public Guid RepositoryId { get; private init; }

    public GitProvider Provider { get; private init; }

    public string DeliveryId { get; private init; } = string.Empty;

    public string Event { get; private init; } = string.Empty;

    public bool SignatureValid { get; private init; }

    public DeliveryOutcome Outcome { get; private set; }

    public string? Message { get; private set; }

    public List<string> LinkedRefs { get; private set; } = [];

    /// <summary>The raw body of a failed delivery.</summary>
    public string? Payload { get; private set; }

    public DateTimeOffset CreatedAt { get; private init; }

    public static WebhookDelivery Create(
        Guid projectId, Guid repositoryId, GitProvider provider, string deliveryId, string gitEvent, bool signatureValid, DeliveryOutcome outcome,
        string? message, IEnumerable<string> linkedRefs, string? payload, DateTimeOffset at) =>
        new()
        {
            ProjectId = projectId,
            RepositoryId = repositoryId,
            Provider = provider,
            DeliveryId = deliveryId.Length > 200 ? deliveryId[..200] : deliveryId,
            Event = gitEvent.Length > 100 ? gitEvent[..100] : gitEvent,
            SignatureValid = signatureValid,
            Outcome = outcome,
            Message = Cut(message),
            LinkedRefs = [.. linkedRefs],
            Payload = outcome == DeliveryOutcome.Failed ? payload : null,
            CreatedAt = at,
        };

    /// <summary>R9: the stored body was processed again and now worked.</summary>
    public void MarkProcessed(IEnumerable<string> linkedRefs, string message)
    {
        Outcome = DeliveryOutcome.Processed;
        Message = Cut(message);
        LinkedRefs = [.. linkedRefs];
        Payload = null;
    }

    /// <summary>R9: processed again and still failed; the message says why.</summary>
    public void RecordRetryFailure(string message) => Message = Cut(message);

    private static string? Cut(string? message) => message is { Length: > MessageMaxLength } ? message[..MessageMaxLength] : message;
}
