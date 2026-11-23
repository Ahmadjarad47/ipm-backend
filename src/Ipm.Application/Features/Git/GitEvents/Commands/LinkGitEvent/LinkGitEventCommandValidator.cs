using FluentValidation;

namespace Ipm.Application.Features.Git.GitEvents.Commands.LinkGitEvent;

internal sealed class LinkGitEventCommandValidator : AbstractValidator<LinkGitEventCommand>
{
    public LinkGitEventCommandValidator() => RuleFor(c => c.Ref).NotEmpty().MaximumLength(30);
}
