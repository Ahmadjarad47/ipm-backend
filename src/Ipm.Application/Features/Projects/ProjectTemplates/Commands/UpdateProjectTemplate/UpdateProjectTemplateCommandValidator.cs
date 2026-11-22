using FluentValidation;
using Ipm.Application.Features.Projects.ProjectTemplates.Common;

namespace Ipm.Application.Features.Projects.ProjectTemplates.Commands.UpdateProjectTemplate;

internal sealed class UpdateProjectTemplateCommandValidator : AbstractValidator<UpdateProjectTemplateCommand>
{
    public UpdateProjectTemplateCommandValidator()
    {
        RuleFor(c => c.Name).TemplateName();
        RuleFor(c => c.Description).TemplateDescription();
        RuleFor(c => c.AllowedTypeIds).NotNull();
        RuleFor(c => c.SuggestedRoleIds).NotNull();
        RuleFor(c => c.SuggestedMembers).NotNull();
    }
}
