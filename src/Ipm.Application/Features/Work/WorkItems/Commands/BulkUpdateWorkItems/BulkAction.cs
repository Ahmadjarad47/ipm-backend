using Ipm.Domain.Work;

namespace Ipm.Application.Features.Work.WorkItems.Commands.BulkUpdateWorkItems;

/// <summary>The one change a bulk request makes to every selected item. <paramref name="Kind"/> is <c>assign</c>, <c>priority</c>, <c>dueDate</c> or <c>transition</c>.</summary>
public sealed record BulkAction(string Kind, IReadOnlyList<Guid>? AssigneeIds, Priority? Priority, DateOnly? DueDate, Guid? ToStatusId, string? Comment);
