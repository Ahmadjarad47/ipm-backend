using Ipm.Application.Abstractions.Auditing;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common.Pagination;

namespace Ipm.Application.Features.Activity.Audit.Queries.GetAuditEvents;

/// <summary>Spec 08 R1 to R4: the audit log as readable events, newest first. <paramref name="From"/> and <paramref name="To"/> are Riyadh days, both included.</summary>
public sealed record GetAuditEventsQuery(
    DateOnly? From,
    DateOnly? To,
    Guid? ActorId,
    string? Action,
    string? SubjectType,
    Guid? SubjectId,
    Guid? ProjectId,
    string? Source,
    string? CorrelationId,
    bool? Security,
    string? Search,
    PageRequest Page) : IQuery<PagedList<AuditEventResponse>>;
