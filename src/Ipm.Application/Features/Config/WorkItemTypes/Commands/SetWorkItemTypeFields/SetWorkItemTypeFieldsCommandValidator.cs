using FluentValidation;
using Ipm.Domain.Config;

namespace Ipm.Application.Features.Config.WorkItemTypes.Commands.SetWorkItemTypeFields;

internal sealed class SetWorkItemTypeFieldsCommandValidator : AbstractValidator<SetWorkItemTypeFieldsCommand>
{
    public SetWorkItemTypeFieldsCommandValidator()
    {
        RuleFor(c => c.Fields).NotNull();

        // R3: the domain rules, reported with the exact path of each problem (fields[1].options…).
        RuleFor(c => c.Fields).Custom((fields, context) =>
        {
            foreach (var (path, message) in FieldDefinitions.Validate(fields ?? []))
            {
                context.AddFailure(path, message);
            }
        });
    }
}
