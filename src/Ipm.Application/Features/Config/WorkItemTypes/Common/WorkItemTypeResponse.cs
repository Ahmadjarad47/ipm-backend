using Ipm.Domain.Config;

namespace Ipm.Application.Features.Config.WorkItemTypes.Common;

/// <summary>One work item type with its custom fields and allowed children. <see cref="Version"/> is sent as the ETag.</summary>
public sealed record WorkItemTypeResponse(
    Guid Id,
    string Key,
    string Name,
    string Description,
    string Color,
    WorkItemIcon Icon,
    int Order,
    bool Active,
    IReadOnlyList<FieldDefinition> Fields,
    IReadOnlyList<Guid> AllowedChildTypeIds,
    uint Version);
