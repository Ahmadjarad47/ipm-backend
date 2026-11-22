using FluentValidation;
using Ipm.Domain.Config;

namespace Ipm.Application.Features.Config.Workflows.Commands.UpdateWorkflow;

internal sealed class UpdateWorkflowCommandValidator : AbstractValidator<UpdateWorkflowCommand>
{
    public UpdateWorkflowCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(Workflow.NameMaxLength);
        RuleFor(c => c.StatusIds).NotNull();
        RuleFor(c => c.Transitions).NotNull();
    }
}
