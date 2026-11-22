using FluentValidation;
using Ipm.Application.Features.Projects.ProjectTemplates.Common;

namespace Ipm.Application.Features.Projects.ProjectTemplates.Commands.CreateProjectTemplate;

internal sealed class CreateProjectTemplateCommandValidator : AbstractValidator<CreateProjectTemplateCommand>
{
    public CreateProjectTemplateCommandValidator()
    {
        RuleFor(c => c.Name).TemplateName();
        RuleFor(c => c.Description).TemplateDescription();
        RuleFor(c => c.AllowedTypeIds).NotNull();
        RuleFor(c => c.SuggestedRoleIds).NotNull();
        RuleFor(c => c.SuggestedMembers).NotNull();
    }
}
