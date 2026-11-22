using FluentValidation;

namespace Ipm.Application.Features.Work.Relations.Commands.CreateRelation;

internal sealed class CreateRelationCommandValidator : AbstractValidator<CreateRelationCommand>
{
    public CreateRelationCommandValidator()
    {
        RuleFor(c => c.SourceId).NotEmpty();
        RuleFor(c => c.TargetId).NotEmpty();
        RuleFor(c => c.RelationTypeId).NotEmpty();
    }
}
