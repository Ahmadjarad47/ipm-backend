using System.Text.Json;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Work.WorkItems.Common;
using Ipm.Domain.Work;

namespace Ipm.Application.Features.Work.WorkItems.Commands.CreateWorkItem;

/// <summary>Creates a work item in the initial status of its workflow (R1 R4 R5 R6).</summary>
public sealed record CreateWorkItemCommand(
    Guid ProjectId,
    Guid TypeId,
    string Title,
    string Description,
    Priority Priority,
    DateOnly? StartDate,
    DateOnly? DueDate,
    IReadOnlyList<Guid> AssigneeIds,
    Guid? ParentId,
    IReadOnlyDictionary<string, JsonElement>? Fields) : ICommand<WorkItemResponse>;
