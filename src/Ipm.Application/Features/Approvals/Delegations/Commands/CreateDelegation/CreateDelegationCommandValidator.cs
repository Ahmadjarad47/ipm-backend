using FluentValidation;
using Ipm.Domain.Approvals;

namespace Ipm.Application.Features.Approvals.Delegations.Commands.CreateDelegation;

internal sealed class CreateDelegationCommandValidator : AbstractValidator<CreateDelegationCommand>
{
    public CreateDelegationCommandValidator()
    {
        RuleFor(c => c.ToUserId).NotEmpty();
        RuleFor(c => c.Reason).NotEmpty().WithMessage("السبب مطلوب.").MaximumLength(Delegation.ReasonMaxLength);
    }
}
