using Ipm.Application.Abstractions.Data;
using Ipm.Domain.Config;
using Ipm.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Work.WorkItems.Common;

internal static class WorkItemWorkflows
{
    /// <summary>The workflow the item follows now. Null when its type has no valid workflow.</summary>
    public static async Task<Workflow?> ForItemAsync(IApplicationDbContext db, Project project, Ipm.Domain.Work.WorkItem item, CancellationToken cancellationToken)
    {
        var type = await db.WorkItemTypes.AsNoTracking().SingleAsync(t => t.Id == item.TypeId, cancellationToken);
        return await ForAsync(db, project, type, cancellationToken);
    }

    /// <summary>The workflow work items of <paramref name="type"/> follow in <paramref name="project"/>: the project override, else the type own. Null when neither exists.</summary>
    public static async Task<Workflow?> ForAsync(IApplicationDbContext db, Project project, WorkItemType type, CancellationToken cancellationToken)
    {
        if (type.WorkflowId is not { } typeWorkflow)
        {
            return null;
        }

        var id = project.EffectiveWorkflowId(type.Id, typeWorkflow);
        return await db.Workflows.AsNoTracking().SingleOrDefaultAsync(w => w.Id == id && w.Scope == StatusScope.WorkItem, cancellationToken);
    }
}
