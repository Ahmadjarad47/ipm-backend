using FluentValidation;
using Ipm.Application.Features.Work.WorkItems.Common;

namespace Ipm.Application.Features.Work.WorkItems.Commands.UpdateWorkItem;

internal sealed class UpdateWorkItemCommandValidator : AbstractValidator<UpdateWorkItemCommand>
{
    public UpdateWorkItemCommandValidator()
    {
        RuleFor(c => c.Title).WorkItemTitle();
        RuleFor(c => c.Description).WorkItemDescription();
        RuleFor(c => c.Priority).WorkItemPriority();
        RuleFor(c => c.AssigneeIds).NotNull();
    }
}
