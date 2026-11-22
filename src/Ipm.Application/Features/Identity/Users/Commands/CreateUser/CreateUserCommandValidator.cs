using FluentValidation;
using Ipm.Application.Features.Identity.Common;
using Ipm.Application.Features.Identity.Users.Common;

namespace Ipm.Application.Features.Identity.Users.Commands.CreateUser;

internal sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(c => c.Username).Username();
        RuleFor(c => c.Name).PersonName();
        RuleFor(c => c.Email).PersonEmail();
        RuleFor(c => c.Title).PersonTitle();
        RuleFor(c => c.SystemRoleIds).NotNull();
        RuleFor(c => c.Password).StrongPassword();
    }
}
