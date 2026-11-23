namespace Ipm.Application.Abstractions.Auditing;

/// <summary>Spec 08 R4. <see cref="From"/> and <see cref="To"/> are instants (the handler turns Riyadh days into them); <see cref="To"/> is exclusive.</summary>
public sealed record AuditFilter(
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    Guid? ActorId = null,
    string? Action = null,
    string? SubjectType = null,
    Guid? SubjectId = null,
    Guid? ProjectId = null,
    Guid? WorkItemId = null,
    string? Source = null,
    string? CorrelationId = null,
    bool? Security = null,
    string? Search = null);
