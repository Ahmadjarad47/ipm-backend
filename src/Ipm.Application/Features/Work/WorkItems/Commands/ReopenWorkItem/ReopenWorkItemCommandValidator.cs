using FluentValidation;
using Ipm.Domain.Work;

namespace Ipm.Application.Features.Work.WorkItems.Commands.ReopenWorkItem;

internal sealed class ReopenWorkItemCommandValidator : AbstractValidator<ReopenWorkItemCommand>
{
    public ReopenWorkItemCommandValidator() =>
        RuleFor(c => c.Comment).NotEmpty().WithMessage("سبب إعادة الفتح مطلوب.").MaximumLength(WorkItemStatusChange.CommentMaxLength);
}
