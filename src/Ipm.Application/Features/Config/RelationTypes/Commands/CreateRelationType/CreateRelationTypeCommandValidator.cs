using FluentValidation;
using Ipm.Application.Features.Config.RelationTypes.Common;

namespace Ipm.Application.Features.Config.RelationTypes.Commands.CreateRelationType;

internal sealed class CreateRelationTypeCommandValidator : AbstractValidator<CreateRelationTypeCommand>
{
    public CreateRelationTypeCommandValidator()
    {
        RuleFor(c => c.Key).NotEmpty().MaximumLength(31);
        RuleFor(c => c.Name).RelationName();
        RuleFor(c => c.InverseName).RelationName();
        RuleFor(c => c.AllowedPairs).NotNull();
    }
}
