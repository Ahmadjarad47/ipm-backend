using FluentValidation;
using Ipm.Domain.Approvals;

namespace Ipm.Application.Features.Approvals.Approvals.Commands.DecideApproval;

internal sealed class DecideApprovalCommandValidator : AbstractValidator<DecideApprovalCommand>
{
    public DecideApprovalCommandValidator()
    {
        RuleFor(c => c.Decision).IsInEnum();
        RuleFor(c => c.Comment).MaximumLength(2000);
        RuleFor(c => c.Revision).GreaterThan(0);
    }
}
