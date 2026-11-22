using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.WorkItemTypes.Common;
using Ipm.Domain.Config;

namespace Ipm.Application.Features.Config.WorkItemTypes.Commands.UpdateWorkItemType;

/// <summary><paramref name="Key"/> is optional and only checked: it can never change (R1). Omitted <paramref name="Order"/> and <paramref name="Active"/> keep their current value. Fields and children have their own commands.</summary>
public sealed record UpdateWorkItemTypeCommand(
    Guid Id,
    string? Key,
    string Name,
    string Description,
    string Color,
    WorkItemIcon Icon,
    int? Order,
    bool? Active,
    uint ExpectedVersion) : ICommand<WorkItemTypeResponse>;
