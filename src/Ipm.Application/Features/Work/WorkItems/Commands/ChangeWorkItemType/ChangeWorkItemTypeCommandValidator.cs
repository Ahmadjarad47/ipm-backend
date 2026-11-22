using FluentValidation;

namespace Ipm.Application.Features.Work.WorkItems.Commands.ChangeWorkItemType;

internal sealed class ChangeWorkItemTypeCommandValidator : AbstractValidator<ChangeWorkItemTypeCommand>
{
    public ChangeWorkItemTypeCommandValidator() => RuleFor(c => c.TypeId).NotEmpty();
}
