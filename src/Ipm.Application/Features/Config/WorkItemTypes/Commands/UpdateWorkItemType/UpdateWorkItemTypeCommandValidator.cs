using FluentValidation;
using Ipm.Application.Features.Config.WorkItemTypes.Common;

namespace Ipm.Application.Features.Config.WorkItemTypes.Commands.UpdateWorkItemType;

internal sealed class UpdateWorkItemTypeCommandValidator : AbstractValidator<UpdateWorkItemTypeCommand>
{
    public UpdateWorkItemTypeCommandValidator()
    {
        RuleFor(c => c.Name).TypeName();
        RuleFor(c => c.Description).TypeDescription();
        RuleFor(c => c.Color).TypeColor();
        RuleFor(c => c.Icon).IsInEnum();
        RuleFor(c => c.Order).GreaterThanOrEqualTo(0).When(c => c.Order.HasValue);
    }
}
