using FluentValidation;
using Ipm.Domain.Work;

namespace Ipm.Application.Features.Work.WorkItems.Queries.GetWorkItems;

internal sealed class GetWorkItemsQueryValidator : AbstractValidator<GetWorkItemsQuery>
{
    public GetWorkItemsQueryValidator() =>
        RuleFor(q => q.Priority)
            .Must(p => p is null || Enum.TryParse<Priority>(p, ignoreCase: true, out _))
            .WithMessage("الأولوية: low أو medium أو high أو critical.");
}
