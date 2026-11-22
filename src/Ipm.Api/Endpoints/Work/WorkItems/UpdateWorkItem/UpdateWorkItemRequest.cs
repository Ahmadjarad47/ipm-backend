using System.Text.Json;
using Ipm.Domain.Work;

namespace Ipm.Api.Endpoints.Work.WorkItems.UpdateWorkItem;

public sealed record UpdateWorkItemRequest(
    string Title,
    string? Description,
    Priority Priority,
    DateOnly? StartDate,
    DateOnly? DueDate,
    IReadOnlyList<Guid>? AssigneeIds,
    Dictionary<string, JsonElement>? Fields,
    Guid? StatusId,
    Guid? TypeId,
    Guid? ParentId);
