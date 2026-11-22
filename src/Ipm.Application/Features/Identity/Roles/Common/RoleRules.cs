using FluentValidation;
using Ipm.Domain.Identity;

namespace Ipm.Application.Features.Identity.Roles.Common;

/// <summary>Field rules shared by the create and update validators.</summary>
internal static class RoleRules
{
    public static IRuleBuilderOptions<T, string> RoleName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(Role.NameMaxLength);

    public static IRuleBuilderOptions<T, string> RoleDescription<T>(this IRuleBuilder<T, string> rule) =>
        rule.MaximumLength(Role.DescriptionMaxLength);
}
