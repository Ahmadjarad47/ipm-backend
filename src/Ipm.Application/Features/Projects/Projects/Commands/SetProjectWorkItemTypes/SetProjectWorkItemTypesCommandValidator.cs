using FluentValidation;

namespace Ipm.Application.Features.Projects.Projects.Commands.SetProjectWorkItemTypes;

internal sealed class SetProjectWorkItemTypesCommandValidator : AbstractValidator<SetProjectWorkItemTypesCommand>
{
    public SetProjectWorkItemTypesCommandValidator() => RuleFor(c => c.AllowedTypeIds).NotNull();
}
