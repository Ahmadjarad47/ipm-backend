using FluentValidation;

namespace Ipm.Application.Features.Identity.Users.Commands.SetUserRoles;

internal sealed class SetUserRolesCommandValidator : AbstractValidator<SetUserRolesCommand>
{
    public SetUserRolesCommandValidator() => RuleFor(c => c.SystemRoleIds).NotNull();
}
