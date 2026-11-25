using Ipm.Application.Abstractions.Data;
using Ipm.Domain.Config;
using Ipm.Domain.Projects;
using Ipm.Domain.Work;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Work.WorkItems.Common;

/// <summary>R10: everything that stops an item from becoming another type, all at once, so nothing is lost silently.</summary>
internal static class TypeChangeAnalysis
{
    public static async Task<IReadOnlyList<TypeChangeConflict>> ConflictsAsync(
        IApplicationDbContext db, Project project, WorkItem item, Guid newTypeId, CancellationToken cancellationToken)
    {
        var newType = await db.WorkItemTypes.AsNoTracking().SingleOrDefaultAsync(t => t.Id == newTypeId, cancellationToken);
        if (newType is not { IsActive: true } || !project.AllowedTypeIds.Contains(newType.Id))
        {
            return [new TypeChangeConflict("typeId", "النوع المطلوب غير موجود أو معطّل أو غير مسموح به في هذا المشروع.")];
        }

        var conflicts = new List<TypeChangeConflict>();

        // The status must exist in the workflow the new type follows in this project.
        var workflow = await WorkItemWorkflows.ForAsync(db, project, newType, cancellationToken);
        if (workflow is null)
        {
            conflicts.Add(new TypeChangeConflict("statusId", "لا يوجد سير عمل صالح للنوع المطلوب في هذا المشروع."));
        }
        else if (!workflow.StatusIds.Contains(item.StatusId))
        {
            conflicts.Add(new TypeChangeConflict("statusId", "حالة العمل الحالية غير موجودة في سير عمل النوع المطلوب."));
        }

        // Custom fields: required ones missing, values that no longer fit, and values that would be dropped.
        foreach (var (path, message) in WorkItemFieldValues.Validate(newType.Fields, item.Fields))
        {
            conflicts.Add(new TypeChangeConflict(path, message));
        }

        // The parent and the active children must still accept the new type.
        if (item.ParentId is { } parentId)
        {
            var parentChildren = await db.WorkItems.AsNoTracking()
                .Where(i => i.Id == parentId)
                .Join(db.WorkItemTypes, i => i.TypeId, t => t.Id, (_, t) => t.AllowedChildTypeIds)
                .SingleOrDefaultAsync(cancellationToken);
            if (parentChildren is not null && !parentChildren.Contains(newType.Id))
            {
                conflicts.Add(new TypeChangeConflict("parentId", "نوع الأب لا يسمح بالنوع المطلوب كابن."));
            }
        }

        var childTypes = await db.WorkItems.AsNoTracking()
            .Where(i => i.ParentId == item.Id && !i.IsArchived)
            .Select(i => i.TypeId)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (childTypes.Exists(t => !newType.AllowedChildTypeIds.Contains(t)))
        {
            conflicts.Add(new TypeChangeConflict("children", "النوع المطلوب لا يسمح ببعض أنواع الأبناء الحاليين."));
        }

        return conflicts;
    }
}
