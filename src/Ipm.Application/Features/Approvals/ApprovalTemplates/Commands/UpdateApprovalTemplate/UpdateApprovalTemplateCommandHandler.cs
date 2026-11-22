using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Approvals.ApprovalTemplates.Common;
using Ipm.Domain.Approvals;
using Ipm.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Approvals.ApprovalTemplates.Commands.UpdateApprovalTemplate;

internal sealed class UpdateApprovalTemplateCommandHandler(IApplicationDbContext db, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateApprovalTemplateCommand, ApprovalTemplateResponse>
{
    public async ValueTask<Result<ApprovalTemplateResponse>> Handle(UpdateApprovalTemplateCommand command, CancellationToken cancellationToken)
    {
        var template = await db.ApprovalTemplates.SingleOrDefaultAsync(t => t.Id == command.Id, cancellationToken);
        if (template is null)
        {
            return ApprovalErrors.TemplateNotFound;
        }

        if (!template.HasVersion(command.ExpectedVersion))
        {
            return Error.StaleVersion;
        }

        if (command.Event != template.Event)
        {
            if (await db.ApprovalRequests.AnyAsync(r => r.TemplateId == template.Id, cancellationToken))
            {
                return ApprovalErrors.EventLocked;
            }

            template.ChangeEvent(command.Event);
        }

        var active = command.Active ?? template.IsActive;
        await ApprovalTemplateChecks.EnsureValidAsync(db, command.Name, command.Event, active, command.Stages, command.ProtectedFields, command.Rule, cancellationToken);

        var updated = template.Update(command.Name, active, command.Stages, command.ProtectedFields, command.Rule, command.AllowSelfApproval);
        if (updated.IsFailure)
        {
            return updated.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return template.ToResponse();
    }
}
