using FluentValidation;
using Ipm.Application.Features.Config.WorkItemTypes.Common;

namespace Ipm.Application.Features.Config.WorkItemTypes.Commands.CreateWorkItemType;

internal sealed class CreateWorkItemTypeCommandValidator : AbstractValidator<CreateWorkItemTypeCommand>
{
    public CreateWorkItemTypeCommandValidator()
    {
        RuleFor(c => c.Key).TypeKey();
        RuleFor(c => c.Name).TypeName();
        RuleFor(c => c.Description).TypeDescription();
        RuleFor(c => c.Color).TypeColor();
        RuleFor(c => c.Icon).IsInEnum();
        RuleFor(c => c.Order).TypeOrder();
    }
}
