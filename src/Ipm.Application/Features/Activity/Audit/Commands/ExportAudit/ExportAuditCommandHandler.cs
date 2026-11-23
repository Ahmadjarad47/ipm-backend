using System.Text.Json;
using Ipm.Application.Abstractions.Auditing;
using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Activity.Common;
using Ipm.Application.Features.Identity.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;

namespace Ipm.Application.Features.Activity.Audit.Commands.ExportAudit;

internal sealed class ExportAuditCommandHandler(IUserContext currentUser, IAccessService access, IAuditFeed feed) : ICommandHandler<ExportAuditCommand, AuditExport>
{
    public async ValueTask<Result<AuditExport>> Handle(ExportAuditCommand command, CancellationToken cancellationToken)
    {
        var caller = await AccessChecks.CallerAsync(currentUser, access, cancellationToken);
        if (caller.IsFailure)
        {
            return caller.Error;
        }

        if (!caller.Value.Has(Permissions.AuditExport))
        {
            return AuthErrors.Forbidden;
        }

        var filter = new AuditFilter(
            AuditAccess.StartOf(command.From), AuditAccess.EndOf(command.To), command.ActorId, command.Action, command.SubjectType, command.SubjectId, command.ProjectId,
            WorkItemId: null, command.Source, command.CorrelationId, command.Security, command.Search);

        // audit.export is a system permission that reads the whole log, like audit.view.
        var count = await feed.CountAsync(filter, AuditVisibility.Everything, cancellationToken);
        if (count > ExportAuditCommand.MaxRows)
        {
            return Error.Failure("Audit.ExportTooLarge", $"النتيجة {count} سطراً وتتجاوز الحد ({ExportAuditCommand.MaxRows}). ضيّق الفلاتر (التاريخ أو المشروع أو الإجراء).");
        }

        await feed.RecordExportAsync(currentUser.UserId, JsonSerializer.Serialize(command with { }), count, cancellationToken);
        return new AuditExport(count, feed.StreamAsync(filter, AuditVisibility.Everything, cancellationToken));
    }
}
