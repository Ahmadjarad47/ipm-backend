using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Activity.Common;

namespace Ipm.Application.Features.Activity.Audit.Commands.ExportAudit;

/// <summary>Spec 08 R8 (audit.export): the audit log with the same filters as the list, without paging, up to <see cref="MaxRows"/> rows.</summary>
public sealed record ExportAuditCommand(
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
    string? Search) : ICommand<AuditExport>
{
    public const int MaxRows = 50_000;
}
