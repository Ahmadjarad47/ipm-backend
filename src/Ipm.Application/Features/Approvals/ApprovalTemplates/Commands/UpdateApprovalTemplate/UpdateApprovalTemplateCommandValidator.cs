using FluentValidation;

namespace Ipm.Application.Features.Approvals.ApprovalTemplates.Commands.UpdateApprovalTemplate;

internal sealed class UpdateApprovalTemplateCommandValidator : AbstractValidator<UpdateApprovalTemplateCommand>
{
    public UpdateApprovalTemplateCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty();
        RuleFor(c => c.Event).IsInEnum();
        RuleFor(c => c.Stages).NotNull();
        RuleFor(c => c.ProtectedFields).NotNull();
    }
}
