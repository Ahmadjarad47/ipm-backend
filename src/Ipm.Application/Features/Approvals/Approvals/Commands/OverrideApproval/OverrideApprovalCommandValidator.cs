using FluentValidation;
using Ipm.Domain.Approvals;

namespace Ipm.Application.Features.Approvals.Approvals.Commands.OverrideApproval;

internal sealed class OverrideApprovalCommandValidator : AbstractValidator<OverrideApprovalCommand>
{
    public OverrideApprovalCommandValidator()
    {
        RuleFor(c => c.Decision).Must(d => d is DecisionKind.Approve or DecisionKind.Reject).WithMessage("القرار المباشر اعتماد أو رفض فقط.");
        RuleFor(c => c.Comment).NotEmpty().MaximumLength(2000);
        RuleFor(c => c.Revision).GreaterThan(0);
    }
}
