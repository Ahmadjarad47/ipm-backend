using FluentValidation;
using Ipm.Domain.Config;

namespace Ipm.Application.Features.Config.RelationTypes.Common;

/// <summary>Field rules shared by the create and update validators. The key format and the direction rules are the domain (R6, R7).</summary>
internal static class RelationTypeRules
{
    public static IRuleBuilderOptions<T, string> RelationName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(RelationType.NameMaxLength);
}
