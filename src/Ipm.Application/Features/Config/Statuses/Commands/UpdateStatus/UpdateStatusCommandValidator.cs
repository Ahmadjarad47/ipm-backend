using FluentValidation;
using Ipm.Application.Features.Config.Statuses.Common;

namespace Ipm.Application.Features.Config.Statuses.Commands.UpdateStatus;

internal sealed class UpdateStatusCommandValidator : AbstractValidator<UpdateStatusCommand>
{
    public UpdateStatusCommandValidator()
    {
        RuleFor(c => c.Name).StatusName();
        RuleFor(c => c.Description).StatusDescription();
        RuleFor(c => c.Color).StatusColor();
        RuleFor(c => c.Category).IsInEnum();
        RuleFor(c => c.Order).GreaterThanOrEqualTo(0).When(c => c.Order.HasValue);
    }
}
