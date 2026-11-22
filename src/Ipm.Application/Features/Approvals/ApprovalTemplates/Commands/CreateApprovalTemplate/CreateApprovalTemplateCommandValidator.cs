using FluentValidation;

namespace Ipm.Application.Features.Approvals.ApprovalTemplates.Commands.CreateApprovalTemplate;

internal sealed class CreateApprovalTemplateCommandValidator : AbstractValidator<CreateApprovalTemplateCommand>
{
    public CreateApprovalTemplateCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty();
        RuleFor(c => c.Event).IsInEnum();
        RuleFor(c => c.Stages).NotNull();
        RuleFor(c => c.ProtectedFields).NotNull();
    }
}
