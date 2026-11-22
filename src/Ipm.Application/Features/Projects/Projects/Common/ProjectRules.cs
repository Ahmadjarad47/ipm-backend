using FluentValidation;
using Ipm.Domain.Projects;

namespace Ipm.Application.Features.Projects.Projects.Common;

/// <summary>Field rules shared by the create and update validators. The code format and the dates order are the domain (R1, R2).</summary>
internal static class ProjectRules
{
    public static IRuleBuilderOptions<T, string> ProjectName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(Project.NameMaxLength);

    public static IRuleBuilderOptions<T, string> ProjectDescription<T>(this IRuleBuilder<T, string> rule) =>
        rule.MaximumLength(4000);

    public static IRuleBuilderOptions<T, string> ProjectCode<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(Project.CodeMaxLength);
}
