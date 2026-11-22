using System.Text.Json;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Work.WorkItems.Common;
using Ipm.Domain.Work;

namespace Ipm.Application.Features.Work.WorkItems.Commands.UpdateWorkItem;

/// <summary>
/// Replaces the plain fields, the assignees and the custom field values (R2). <paramref name="StatusId"/>, <paramref name="TypeId"/> and
/// <paramref name="ParentId"/> only exist so a client can send the whole item back: they are accepted when they equal the current
/// value and refused otherwise, because each has its own operation.
/// </summary>
public sealed record UpdateWorkItemCommand(
    Guid Id,
    string Title,
    string Description,
    Priority Priority,
    DateOnly? StartDate,
    DateOnly? DueDate,
    IReadOnlyList<Guid> AssigneeIds,
    IReadOnlyDictionary<string, JsonElement>? Fields,
    Guid? StatusId,
    Guid? TypeId,
    Guid? ParentId,
    uint ExpectedVersion) : ICommand<WorkItemResponse>;
