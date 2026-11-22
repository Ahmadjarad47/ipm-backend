using FluentValidation;
using Ipm.Domain.Work;

namespace Ipm.Application.Features.Work.WorkItems.Commands.TransitionWorkItem;

internal sealed class TransitionWorkItemCommandValidator : AbstractValidator<TransitionWorkItemCommand>
{
    public TransitionWorkItemCommandValidator()
    {
        RuleFor(c => c.ToStatusId).NotEmpty();
        RuleFor(c => c.Comment).MaximumLength(WorkItemStatusChange.CommentMaxLength);
    }
}
