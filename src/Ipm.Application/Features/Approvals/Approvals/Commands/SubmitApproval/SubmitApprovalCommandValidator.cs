using FluentValidation;

namespace Ipm.Application.Features.Approvals.Approvals.Commands.SubmitApproval;

internal sealed class SubmitApprovalCommandValidator : AbstractValidator<SubmitApprovalCommand>
{
    public SubmitApprovalCommandValidator()
    {
        RuleFor(c => c.TemplateId).NotEmpty();
        RuleFor(c => c.SubjectType).IsInEnum();
        RuleFor(c => c.SubjectId).NotEmpty();
        RuleFor(c => c.Action).IsInEnum();
    }
}
