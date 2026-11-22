using FluentValidation;
using Ipm.Domain.Projects;

namespace Ipm.Application.Features.Projects.Projects.Commands.TransitionProject;

internal sealed class TransitionProjectCommandValidator : AbstractValidator<TransitionProjectCommand>
{
    public TransitionProjectCommandValidator()
    {
        RuleFor(c => c.ToStatusId).NotEmpty();
        RuleFor(c => c.Comment).MaximumLength(ProjectStatusChange.CommentMaxLength);
    }
}
