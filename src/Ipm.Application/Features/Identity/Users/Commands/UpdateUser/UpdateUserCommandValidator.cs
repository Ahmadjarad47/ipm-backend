using FluentValidation;
using Ipm.Application.Features.Identity.Users.Common;

namespace Ipm.Application.Features.Identity.Users.Commands.UpdateUser;

internal sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(c => c.Name).PersonName();
        RuleFor(c => c.Email).PersonEmail();
        RuleFor(c => c.Title).PersonTitle();
    }
}
