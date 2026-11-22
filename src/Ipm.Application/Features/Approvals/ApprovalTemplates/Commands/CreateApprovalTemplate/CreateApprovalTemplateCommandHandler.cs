using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Approvals.ApprovalTemplates.Common;
using Ipm.Domain.Approvals;
using Ipm.Domain.Common;

namespace Ipm.Application.Features.Approvals.ApprovalTemplates.Commands.CreateApprovalTemplate;

internal sealed class CreateApprovalTemplateCommandHandler(IApplicationDbContext db, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateApprovalTemplateCommand, ApprovalTemplateResponse>
{
    public async ValueTask<Result<ApprovalTemplateResponse>> Handle(CreateApprovalTemplateCommand command, CancellationToken cancellationToken)
    {
        var active = command.Active ?? true;
        await ApprovalTemplateChecks.EnsureValidAsync(db, command.Name, command.Event, active, command.Stages, command.ProtectedFields, command.Rule, cancellationToken);

        var created = ApprovalTemplate.Create(command.Name, command.Event, active, command.Stages, command.ProtectedFields, command.Rule, command.AllowSelfApproval);
        if (created.IsFailure)
        {
            return created.Error;
        }

        db.ApprovalTemplates.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return created.Value.ToResponse();
    }
}
