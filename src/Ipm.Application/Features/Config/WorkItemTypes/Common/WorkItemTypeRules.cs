using FluentValidation;
using Ipm.Domain.Config;

namespace Ipm.Application.Features.Config.WorkItemTypes.Common;

/// <summary>Field rules shared by the create and update validators.</summary>
internal static class WorkItemTypeRules
{
    /// <summary>The format of the key is checked by the domain (R1) so a bad key and a repeated key both read as the domain says.</summary>
    public static IRuleBuilderOptions<T, string> TypeKey<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(WorkItemType.KeyMaxLength);

    public static IRuleBuilderOptions<T, string> TypeName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(WorkItemType.NameMaxLength);

    public static IRuleBuilderOptions<T, string> TypeDescription<T>(this IRuleBuilder<T, string> rule) =>
        rule.MaximumLength(WorkItemType.DescriptionMaxLength);

    public static IRuleBuilderOptions<T, string> TypeColor<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().Must(WorkItemType.IsValidColor).WithMessage("اللون يجب أن يكون بصيغة #RRGGBB.");

    public static IRuleBuilderOptions<T, int> TypeOrder<T>(this IRuleBuilder<T, int> rule) =>
        rule.GreaterThanOrEqualTo(0);
}
