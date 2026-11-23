using FluentValidation;

namespace Ipm.Application.Features.Git.GitEvents.Queries.GetGitEvents;

internal sealed class GetGitEventsQueryValidator : AbstractValidator<GetGitEventsQuery>
{
    public GetGitEventsQueryValidator() =>
        RuleFor(q => q.Kind).Must(k => k is null or "push" or "pull-request" or "merge").WithMessage("النوع: push أو pull-request أو merge.");
}
