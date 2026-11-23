namespace Ipm.Application.Abstractions.Auditing;

/// <summary>R1: one event of the audit log or of an activity feed. <see cref="Summary"/> is a ready Arabic sentence for display.</summary>
public sealed record AuditEventResponse(
    Guid Id,
    long Seq,
    DateTimeOffset At,
    Guid? ActorId,
    string? ActorName,
    string Action,
    string SubjectType,
    Guid SubjectId,
    string? SubjectLabel,
    Guid? ProjectId,
    IReadOnlyList<AuditFieldChange> Changes,
    string Source,
    string CorrelationId,
    bool Security,
    string Summary);
