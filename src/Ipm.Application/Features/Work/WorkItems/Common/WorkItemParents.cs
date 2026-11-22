using Ipm.Application.Abstractions.Data;
using Ipm.Domain.Common;
using Ipm.Domain.Work;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Work.WorkItems.Common;

/// <summary>R6: whether an item may sit under a parent.</summary>
internal static class WorkItemParents
{
    private const int MaxDepth = 200;

    /// <summary>
    /// The parent exists, is in the same project, is not archived, its type allows the item type as a child, and (for an existing item)
    /// it is neither the item nor one of its descendants.
    /// </summary>
    public static async Task<Result> EnsureAsync(
        IApplicationDbContext db, Guid projectId, Guid itemTypeId, Guid? itemId, Guid parentId, CancellationToken cancellationToken)
    {
        var parent = await db.WorkItems.AsNoTracking().SingleOrDefaultAsync(i => i.Id == parentId, cancellationToken);
        if (parent is null)
        {
            return WorkItemErrors.ParentNotFound;
        }

        if (parent.ProjectId != projectId)
        {
            return WorkItemErrors.ParentDifferentProject;
        }

        if (itemId is { } id)
        {
            var cycle = await IsSelfOrDescendantAsync(db, id, parent, cancellationToken);
            if (cycle)
            {
                return WorkItemErrors.ParentCycle;
            }
        }

        if (parent.IsArchived)
        {
            return WorkItemErrors.ParentArchived;
        }

        var allowedChildren = await db.WorkItemTypes.AsNoTracking()
            .Where(t => t.Id == parent.TypeId)
            .Select(t => t.AllowedChildTypeIds)
            .SingleOrDefaultAsync(cancellationToken);
        return allowedChildren is not null && allowedChildren.Contains(itemTypeId) ? Result.Success() : WorkItemErrors.ParentTypeNotAllowed;
    }

    /// <summary>Walks up from the wanted parent; meeting the item itself means the parent is the item or one of its descendants.</summary>
    private static async Task<bool> IsSelfOrDescendantAsync(IApplicationDbContext db, Guid itemId, WorkItem parent, CancellationToken cancellationToken)
    {
        Guid? current = parent.Id;
        for (var depth = 0; current is { } id && depth < MaxDepth; depth++)
        {
            if (id == itemId)
            {
                return true;
            }

            current = await db.WorkItems.AsNoTracking().Where(i => i.Id == id).Select(i => i.ParentId).SingleOrDefaultAsync(cancellationToken);
        }

        return false;
    }
}
