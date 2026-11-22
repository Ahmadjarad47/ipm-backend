using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.WorkItemTypes.Common;
using Ipm.Domain.Config;

namespace Ipm.Application.Features.Config.WorkItemTypes.Commands.CreateWorkItemType;

/// <summary>A new type starts with no custom fields and no allowed children; they have their own commands. Without a workflow it gets the standard one (R14).</summary>
public sealed record CreateWorkItemTypeCommand(
    string Key,
    string Name,
    string Description,
    string Color,
    WorkItemIcon Icon,
    int Order,
    bool Active,
    Guid? WorkflowId) : ICommand<WorkItemTypeResponse>;
