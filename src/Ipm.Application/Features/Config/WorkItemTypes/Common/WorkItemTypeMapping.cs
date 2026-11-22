using Ipm.Domain.Config;

namespace Ipm.Application.Features.Config.WorkItemTypes.Common;

internal static class WorkItemTypeMapping
{
    public static WorkItemTypeResponse ToResponse(this WorkItemType type) => new(
        type.Id,
        type.Key,
        type.Name,
        type.Description,
        type.Color,
        type.Icon,
        type.SortOrder,
        type.IsActive,
        type.Fields,
        type.AllowedChildTypeIds,
        type.Version);
}
