using Ipm.Domain.Work;

namespace Ipm.Application.Features.Work.WorkItems.Common;

internal static class WorkItemMapping
{
    public static WorkItemResponse ToResponse(this WorkItem item) => new(
        item.Id,
        item.Ref,
        item.ProjectId,
        item.TypeId,
        item.Title,
        item.Description,
        item.StatusId,
        item.Priority,
        item.AssigneeIds,
        item.StartDate,
        item.DueDate,
        item.ParentId,
        item.Fields,
        item.IsArchived,
        item.CreatedBy,
        item.CreatedAt,
        item.Version);
}
