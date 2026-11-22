using FluentValidation;
using Ipm.Application.Features.Identity.Common;

namespace Ipm.Application.Features.Identity.Auth.Commands.ChangeMyPassword;

internal sealed class ChangeMyPasswordCommandValidator : AbstractValidator<ChangeMyPasswordCommand>
{
    public ChangeMyPasswordCommandValidator()
    {
        RuleFor(c => c.CurrentPassword).NotEmpty();
        RuleFor(c => c.NewPassword).StrongPassword()
            .NotEqual(c => c.CurrentPassword).WithMessage("كلمة المرور الجديدة يجب أن تختلف عن الحالية.");
    }
}
