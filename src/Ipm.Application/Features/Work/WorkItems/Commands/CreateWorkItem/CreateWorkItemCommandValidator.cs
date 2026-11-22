using FluentValidation;
using Ipm.Application.Features.Work.WorkItems.Common;

namespace Ipm.Application.Features.Work.WorkItems.Commands.CreateWorkItem;

internal sealed class CreateWorkItemCommandValidator : AbstractValidator<CreateWorkItemCommand>
{
    public CreateWorkItemCommandValidator()
    {
        RuleFor(c => c.ProjectId).NotEmpty();
        RuleFor(c => c.TypeId).NotEmpty();
        RuleFor(c => c.Title).WorkItemTitle();
        RuleFor(c => c.Description).WorkItemDescription();
        RuleFor(c => c.Priority).WorkItemPriority();
        RuleFor(c => c.AssigneeIds).NotNull();
    }
}
