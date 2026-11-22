using FluentValidation;
using Ipm.Application.Features.Projects.Projects.Common;

namespace Ipm.Application.Features.Projects.Projects.Commands.CreateProject;

internal sealed class CreateProjectCommandValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectCommandValidator()
    {
        RuleFor(c => c.Code).ProjectCode();
        RuleFor(c => c.Name).ProjectName();
        RuleFor(c => c.Description).ProjectDescription();
        RuleFor(c => c.OwnerId).NotEmpty();
    }
}
