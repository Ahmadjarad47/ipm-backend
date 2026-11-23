using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Features.Work.WorkItems.Common;

namespace Ipm.Application.Features.Work.WorkItems.Queries.GetWorkItems;

/// <summary>
/// The work items of the projects the caller can see (tasks.view). The type, status, priority and assignee filters take several values (any of them); empty means no filter. <paramref name="Search"/> matches the ref or the title.
/// <paramref name="Overdue"/> keeps items due before today that are not done or cancelled. <paramref name="Sort"/> is <c>ref</c>,
/// <c>title</c>, <c>priority</c>, <c>dueDate</c>, <c>status</c>, <c>type</c> or <c>createdAt</c>.
/// </summary>
public sealed record GetWorkItemsQuery(
    Guid? ProjectId,
    IReadOnlyList<Guid> TypeIds,
    IReadOnlyList<Guid> StatusIds,
    IReadOnlyList<string> Priorities,
    IReadOnlyList<Guid> AssigneeIds,
    bool Unassigned,
    Guid? ParentId,
    bool? HasChildren,
    bool? HasDependency,
    bool? Unblocked,
    Guid? BlockedBy,
    Guid? Blocking,
    string? Search,
    bool Overdue,
    bool IncludeArchived,
    DateOnly? CreatedFrom,
    DateOnly? CreatedTo,
    DateOnly? DueFrom,
    DateOnly? DueTo,
    string? Sort,
    bool Descending,
    PageRequest Page) : IQuery<PagedList<WorkItemResponse>>;
