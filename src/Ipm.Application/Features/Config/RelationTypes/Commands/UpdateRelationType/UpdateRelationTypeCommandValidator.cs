using FluentValidation;
using Ipm.Application.Features.Config.RelationTypes.Common;

namespace Ipm.Application.Features.Config.RelationTypes.Commands.UpdateRelationType;

internal sealed class UpdateRelationTypeCommandValidator : AbstractValidator<UpdateRelationTypeCommand>
{
    public UpdateRelationTypeCommandValidator()
    {
        RuleFor(c => c.Name).RelationName();
        RuleFor(c => c.InverseName).RelationName();
        RuleFor(c => c.AllowedPairs).NotNull();
    }
}
