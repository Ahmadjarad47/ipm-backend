using Ipm.Domain.Config;

namespace Ipm.Api.Endpoints.Config.WorkItemTypes.UpdateWorkItemType;

/// <summary><c>key</c> is optional and only checked (a different key is refused). Omitted <c>order</c> and <c>active</c> keep their value.</summary>
public sealed record UpdateWorkItemTypeRequest(
    string Name,
    string? Description,
    string Color,
    WorkItemIcon Icon,
    int? Order,
    bool? Active,
    string? Key);
