using FluentValidation;
using Ipm.Domain.Work;

namespace Ipm.Application.Features.Work.WorkItems.Common;

/// <summary>Field rules shared by the create and update validators. The dates order is the domain (R1).</summary>
internal static class WorkItemRules
{
    public static IRuleBuilderOptions<T, string> WorkItemTitle<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(WorkItem.TitleMaxLength);

    public static IRuleBuilderOptions<T, string> WorkItemDescription<T>(this IRuleBuilder<T, string> rule) =>
        rule.MaximumLength(WorkItem.DescriptionMaxLength);

    public static IRuleBuilderOptions<T, Priority> WorkItemPriority<T>(this IRuleBuilder<T, Priority> rule) =>
        rule.IsInEnum();
}
