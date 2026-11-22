using FluentValidation;
using Ipm.Application.Features.Identity.Common;

namespace Ipm.Application.Features.Identity.Users.Commands.ResetUserPassword;

internal sealed class ResetUserPasswordCommandValidator : AbstractValidator<ResetUserPasswordCommand>
{
    public ResetUserPasswordCommandValidator() => RuleFor(c => c.NewPassword).StrongPassword();
}
