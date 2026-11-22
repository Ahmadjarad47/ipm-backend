using FluentValidation;
using Ipm.Domain.Config;

namespace Ipm.Application.Features.Config.Workflows.Commands.CreateWorkflow;

internal sealed class CreateWorkflowCommandValidator : AbstractValidator<CreateWorkflowCommand>
{
    public CreateWorkflowCommandValidator()
    {
        RuleFor(c => c.Key).NotEmpty().MaximumLength(Workflow.KeyMaxLength);
        RuleFor(c => c.Name).NotEmpty().MaximumLength(Workflow.NameMaxLength);
        RuleFor(c => c.Scope).IsInEnum();
        RuleFor(c => c.StatusIds).NotNull();
        RuleFor(c => c.Transitions).NotNull();
    }
}
