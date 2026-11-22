using FluentValidation;
using Ipm.Application.Features.Identity.Roles.Common;

namespace Ipm.Application.Features.Identity.Roles.Commands.UpdateRole;

internal sealed class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleCommandValidator()
    {
        RuleFor(c => c.Name).RoleName();
        RuleFor(c => c.Description).RoleDescription();
        RuleFor(c => c.Permissions).NotNull();
        RuleFor(c => c.Denied).NotNull();
    }
}
