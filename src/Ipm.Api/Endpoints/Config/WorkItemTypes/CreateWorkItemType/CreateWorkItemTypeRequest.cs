using Ipm.Domain.Config;

namespace Ipm.Api.Endpoints.Config.WorkItemTypes.CreateWorkItemType;

public sealed record CreateWorkItemTypeRequest(
    string Key,
    string Name,
    string? Description,
    string Color,
    WorkItemIcon Icon,
    int? Order,
    bool? Active,
    Guid? WorkflowId);
