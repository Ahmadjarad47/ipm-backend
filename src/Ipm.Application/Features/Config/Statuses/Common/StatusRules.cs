using FluentValidation;
using Ipm.Domain.Config;

namespace Ipm.Application.Features.Config.Statuses.Common;

/// <summary>Field rules shared by the create and update validators. The key format is the domain (R1).</summary>
internal static class StatusRules
{
    public static IRuleBuilderOptions<T, string> StatusName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(Status.NameMaxLength);

    public static IRuleBuilderOptions<T, string> StatusDescription<T>(this IRuleBuilder<T, string> rule) =>
        rule.MaximumLength(Status.DescriptionMaxLength);

    public static IRuleBuilderOptions<T, string> StatusColor<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().Must(Status.IsValidColor).WithMessage("اللون يجب أن يكون بصيغة #RRGGBB.");
}
