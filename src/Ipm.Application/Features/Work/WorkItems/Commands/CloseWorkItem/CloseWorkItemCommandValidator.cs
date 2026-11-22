using FluentValidation;
using Ipm.Domain.Work;

namespace Ipm.Application.Features.Work.WorkItems.Commands.CloseWorkItem;

internal sealed class CloseWorkItemCommandValidator : AbstractValidator<CloseWorkItemCommand>
{
    public CloseWorkItemCommandValidator() => RuleFor(c => c.Comment).MaximumLength(WorkItemStatusChange.CommentMaxLength);
}
