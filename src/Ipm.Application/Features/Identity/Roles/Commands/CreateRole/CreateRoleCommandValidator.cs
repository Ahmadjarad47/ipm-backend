using FluentValidation;
using Ipm.Application.Features.Identity.Roles.Common;

namespace Ipm.Application.Features.Identity.Roles.Commands.CreateRole;

internal sealed class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(c => c.Name).RoleName();
        RuleFor(c => c.Description).RoleDescription();
        RuleFor(c => c.Scope).IsInEnum();
        RuleFor(c => c.Permissions).NotNull();
        RuleFor(c => c.Denied).NotNull();
    }
}
