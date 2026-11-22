using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Features.Identity.Common;
using Ipm.Application.Features.Work.WorkItems.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Ipm.Domain.Identity;
using Ipm.Domain.Work;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Work.WorkItems.Queries.GetWorkItems;

internal sealed class GetWorkItemsQueryHandler(IApplicationDbContext db, IUserContext currentUser, IAccessService access, TimeProvider time)
    : IQueryHandler<GetWorkItemsQuery, PagedList<WorkItemResponse>>
{
    public async ValueTask<Result<PagedList<WorkItemResponse>>> Handle(GetWorkItemsQuery query, CancellationToken cancellationToken)
    {
        var caller = await AccessChecks.CallerAsync(currentUser, access, cancellationToken);
        if (caller.IsFailure)
        {
            return caller.Error;
        }

        IQueryable<WorkItem> items = db.WorkItems.AsNoTracking();

        // A holder of tasks.view in the system sees every project items; anyone else only those of projects whose role there grants it.
        if (!caller.Value.Has(Permissions.TasksView))
        {
            var visible = caller.Value.Denied.Contains(Permissions.TasksView)
                ? []
                : caller.Value.ProjectPermissions.Where(p => p.Value.Contains(Permissions.TasksView)).Select(p => p.Key).ToList();
            items = items.Where(i => visible.Contains(i.ProjectId));
        }

        items = Filter(items, query, RiyadhClock.Today(time));

        var total = await items.CountAsync(cancellationToken);
        var rows = await Sort(items, query).Skip(query.Page.Skip).Take(query.Page.Take).ToListAsync(cancellationToken);
        return new PagedList<WorkItemResponse>([.. rows.Select(i => i.ToResponse())], Math.Max(query.Page.Page, 1), query.Page.Take, total);
    }

    private IQueryable<WorkItem> Filter(IQueryable<WorkItem> items, GetWorkItemsQuery query, DateOnly today)
    {
        if (!query.IncludeArchived)
        {
            items = items.Where(i => !i.IsArchived);
        }

        if (query.ProjectId is { } projectId)
        {
            items = items.Where(i => i.ProjectId == projectId);
        }

        if (query.TypeId is { } typeId)
        {
            items = items.Where(i => i.TypeId == typeId);
        }

        if (query.StatusId is { } statusId)
        {
            items = items.Where(i => i.StatusId == statusId);
        }

        if (query.Priority is { } text && Enum.TryParse<Priority>(text, ignoreCase: true, out var priority))
        {
            items = items.Where(i => i.Priority == priority);
        }

        if (query.AssigneeId is { } assigneeId)
        {
            items = items.Where(i => i.AssigneeIds.Contains(assigneeId));
        }

        if (query.Unassigned)
        {
            items = items.Where(i => i.AssigneeIds.Count == 0);
        }

        if (query.ParentId is { } parentId)
        {
            items = items.Where(i => i.ParentId == parentId);
        }

        if (query.HasChildren is { } hasChildren)
        {
            items = hasChildren
                ? items.Where(i => db.WorkItems.Any(c => c.ParentId == i.Id && !c.IsArchived))
                : items.Where(i => !db.WorkItems.Any(c => c.ParentId == i.Id && !c.IsArchived));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            var upper = term.ToUpperInvariant();
            items = items.Where(i => i.Ref.Contains(upper) || i.Title.Contains(term));
        }

        if (query.Overdue)
        {
            items = items.Where(i => i.DueDate < today
                && !db.Statuses.Any(s => s.Id == i.StatusId && (s.Category == StatusCategory.Done || s.Category == StatusCategory.Cancelled)));
        }

        if (query.CreatedFrom is { } createdFrom)
        {
            var from = new DateTimeOffset(createdFrom.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            items = items.Where(i => i.CreatedAt >= from);
        }

        if (query.CreatedTo is { } createdTo)
        {
            var to = new DateTimeOffset(createdTo.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            items = items.Where(i => i.CreatedAt < to);
        }

        if (query.DueFrom is { } dueFrom)
        {
            items = items.Where(i => i.DueDate >= dueFrom);
        }

        if (query.DueTo is { } dueTo)
        {
            items = items.Where(i => i.DueDate <= dueTo);
        }

        return items;
    }

    private IQueryable<WorkItem> Sort(IQueryable<WorkItem> items, GetWorkItemsQuery query)
    {
        var d = query.Descending;
        IOrderedQueryable<WorkItem> ordered = (query.Sort?.ToLowerInvariant()) switch
        {
            "title" => d ? items.OrderByDescending(i => i.Title) : items.OrderBy(i => i.Title),
            "priority" => d ? items.OrderByDescending(PriorityRank) : items.OrderBy(PriorityRank),
            "duedate" => d ? items.OrderByDescending(i => i.DueDate) : items.OrderBy(i => i.DueDate),
            "status" => d
                ? items.OrderByDescending(i => db.Statuses.Where(s => s.Id == i.StatusId).Select(s => s.SortOrder).FirstOrDefault())
                : items.OrderBy(i => db.Statuses.Where(s => s.Id == i.StatusId).Select(s => s.SortOrder).FirstOrDefault()),
            "type" => d
                ? items.OrderByDescending(i => db.WorkItemTypes.Where(t => t.Id == i.TypeId).Select(t => t.SortOrder).FirstOrDefault())
                : items.OrderBy(i => db.WorkItemTypes.Where(t => t.Id == i.TypeId).Select(t => t.SortOrder).FirstOrDefault()),
            "createdat" => d ? items.OrderByDescending(i => i.CreatedAt) : items.OrderBy(i => i.CreatedAt),
            _ => d ? items.OrderByDescending(i => i.Ref) : items.OrderBy(i => i.Ref),
        };

        return ordered.ThenBy(i => i.Id);
    }

    private static readonly System.Linq.Expressions.Expression<Func<WorkItem, int>> PriorityRank = i =>
        i.Priority == Priority.Critical ? 3 : i.Priority == Priority.High ? 2 : i.Priority == Priority.Medium ? 1 : 0;
}
