using FluentValidation;
using Ipm.Domain.Identity;

namespace Ipm.Application.Features.Identity.Auth.Commands.Login;

internal sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(c => c.Username).NotEmpty().MaximumLength(SecurityEvent.UsernameMaxLength);
        RuleFor(c => c.Password).NotEmpty().MaximumLength(128);
    }
}
