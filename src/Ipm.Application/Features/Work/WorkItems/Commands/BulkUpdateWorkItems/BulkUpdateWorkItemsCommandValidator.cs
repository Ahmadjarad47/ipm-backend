using FluentValidation;
using Ipm.Domain.Work;

namespace Ipm.Application.Features.Work.WorkItems.Commands.BulkUpdateWorkItems;

internal sealed class BulkUpdateWorkItemsCommandValidator : AbstractValidator<BulkUpdateWorkItemsCommand>
{
    public static readonly string[] Kinds = ["assign", "priority", "dueDate", "transition"];

    public BulkUpdateWorkItemsCommandValidator()
    {
        RuleFor(c => c.Ids).NotNull();
        RuleFor(c => c.Action).NotNull();
        RuleFor(c => c.Action.Kind).Must(k => Kinds.Contains(k)).WithMessage("نوع الإجراء الجماعي: assign أو priority أو dueDate أو transition.");
        RuleFor(c => c.Action.Comment).MaximumLength(WorkItemStatusChange.CommentMaxLength);
        When(c => c.Action.Kind == "assign", () => RuleFor(c => c.Action.AssigneeIds).NotNull());
        When(c => c.Action.Kind == "priority", () => RuleFor(c => c.Action.Priority).NotNull().IsInEnum());
        When(c => c.Action.Kind == "transition", () => RuleFor(c => c.Action.ToStatusId).NotNull());
    }
}
