using System.Text.Json;
using Ipm.Domain.Work;

namespace Ipm.Application.Features.Work.WorkItems.Common;

/// <summary>One work item. <see cref="Version"/> is sent as the ETag and moves with every change of the item.</summary>
public sealed record WorkItemResponse(
    Guid Id,
    string Ref,
    Guid ProjectId,
    Guid TypeId,
    string Title,
    string Description,
    Guid StatusId,
    Priority Priority,
    IReadOnlyList<Guid> AssigneeIds,
    DateOnly? StartDate,
    DateOnly? DueDate,
    Guid? ParentId,
    IReadOnlyDictionary<string, JsonElement> Fields,
    bool Archived,
    Guid? CreatedBy,
    DateTimeOffset CreatedAt,
    uint Version,
    DevIndicator? Dev = null);
