using FluentValidation;

namespace Ipm.Application.Features.Config.WorkItemTypes.Commands.SetWorkItemTypeChildren;

internal sealed class SetWorkItemTypeChildrenCommandValidator : AbstractValidator<SetWorkItemTypeChildrenCommand>
{
    public SetWorkItemTypeChildrenCommandValidator() => RuleFor(c => c.ChildTypeIds).NotNull();
}
