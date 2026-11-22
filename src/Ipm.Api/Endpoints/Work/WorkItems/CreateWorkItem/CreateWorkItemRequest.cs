using System.Text.Json;
using Ipm.Domain.Work;

namespace Ipm.Api.Endpoints.Work.WorkItems.CreateWorkItem;

public sealed record CreateWorkItemRequest(
    Guid ProjectId,
    Guid TypeId,
    string Title,
    string? Description,
    Priority? Priority,
    DateOnly? StartDate,
    DateOnly? DueDate,
    IReadOnlyList<Guid>? AssigneeIds,
    Guid? ParentId,
    Dictionary<string, JsonElement>? Fields);
