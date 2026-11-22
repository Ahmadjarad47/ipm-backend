using FluentValidation;
using Ipm.Domain.Projects;

namespace Ipm.Application.Features.Projects.ProjectTemplates.Common;

/// <summary>Field rules shared by the create and update validators.</summary>
internal static class ProjectTemplateRules
{
    public static IRuleBuilderOptions<T, string> TemplateName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(ProjectTemplate.NameMaxLength);

    public static IRuleBuilderOptions<T, string> TemplateDescription<T>(this IRuleBuilder<T, string> rule) =>
        rule.MaximumLength(ProjectTemplate.DescriptionMaxLength);
}
