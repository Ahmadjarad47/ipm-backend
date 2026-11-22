using FluentValidation;

namespace Ipm.Application.Features.Projects.Projects.Commands.SetProjectWorkflowOverrides;

internal sealed class SetProjectWorkflowOverridesCommandValidator : AbstractValidator<SetProjectWorkflowOverridesCommand>
{
    public SetProjectWorkflowOverridesCommandValidator() => RuleFor(c => c.Overrides).NotNull();
}
