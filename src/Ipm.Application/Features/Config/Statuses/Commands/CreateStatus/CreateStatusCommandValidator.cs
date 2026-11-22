using FluentValidation;
using Ipm.Application.Features.Config.Statuses.Common;
using Ipm.Domain.Config;

namespace Ipm.Application.Features.Config.Statuses.Commands.CreateStatus;

internal sealed class CreateStatusCommandValidator : AbstractValidator<CreateStatusCommand>
{
    public CreateStatusCommandValidator()
    {
        RuleFor(c => c.Key).NotEmpty().MaximumLength(Status.KeyMaxLength);
        RuleFor(c => c.Name).StatusName();
        RuleFor(c => c.Description).StatusDescription();
        RuleFor(c => c.Color).StatusColor();
        RuleFor(c => c.Category).IsInEnum();
        RuleFor(c => c.Scope).IsInEnum();
        RuleFor(c => c.Order).GreaterThanOrEqualTo(0);
    }
}
