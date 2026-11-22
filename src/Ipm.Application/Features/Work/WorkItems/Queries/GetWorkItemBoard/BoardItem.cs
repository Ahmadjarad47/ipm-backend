using System.Text.Json;
using Ipm.Domain.Work;

namespace Ipm.Application.Features.Work.WorkItems.Queries.GetWorkItemBoard;

/// <summary>A card: only the custom fields the type marks <c>showOnCard</c> are included.</summary>
public sealed record BoardItem(
    Guid Id,
    string Ref,
    Guid TypeId,
    string Title,
    Guid StatusId,
    Priority Priority,
    IReadOnlyList<Guid> AssigneeIds,
    DateOnly? DueDate,
    Guid? ParentId,
    IReadOnlyDictionary<string, JsonElement> Fields,
    uint Version);
