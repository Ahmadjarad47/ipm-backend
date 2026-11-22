using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Projects.Projects.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Ipm.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Work.WorkItems.Queries.GetWorkItemBoard;

internal sealed class GetWorkItemBoardQueryHandler(IApplicationDbContext db, IUserContext currentUser, IAccessService access, ISecurityEventLog securityLog)
    : IQueryHandler<GetWorkItemBoardQuery, BoardResponse>
{
    public async ValueTask<Result<BoardResponse>> Handle(GetWorkItemBoardQuery query, CancellationToken cancellationToken)
    {
        var guarded = await ProjectGuard.ReadWithAsync(db, currentUser, access, securityLog, query.ProjectId, Permissions.TasksView, cancellationToken);
        if (guarded.IsFailure)
        {
            return guarded.Error;
        }

        var project = guarded.Value.Project;
        var types = await db.WorkItemTypes.AsNoTracking().Where(t => project.AllowedTypeIds.Contains(t.Id)).ToListAsync(cancellationToken);
        var workflowIds = types.Where(t => t.WorkflowId != null).Select(t => project.EffectiveWorkflowId(t.Id, t.WorkflowId!.Value)).Distinct().ToList();
        var workflows = await db.Workflows.AsNoTracking().Where(w => workflowIds.Contains(w.Id)).ToDictionaryAsync(w => w.Id, cancellationToken);

        var boardWorkflows = types
            .Where(t => t.WorkflowId != null)
            .Select(t => (Type: t, WorkflowId: project.EffectiveWorkflowId(t.Id, t.WorkflowId!.Value)))
            .Where(x => workflows.ContainsKey(x.WorkflowId))
            .Select(x => new BoardWorkflow(x.Type.Id, x.WorkflowId, workflows[x.WorkflowId].StatusIds))
            .ToList();

        // Items of a type that is no longer allowed still show, with the fields their type marked for the card.
        var cardFields = (await db.WorkItemTypes.AsNoTracking().ToListAsync(cancellationToken))
            .ToDictionary(t => t.Id, t => t.Fields.Where(f => f.ShowOnCard).Select(f => f.Key).ToHashSet());

        var rows = await db.WorkItems.AsNoTracking()
            .Where(i => i.ProjectId == project.Id && !i.IsArchived)
            .OrderBy(i => i.Number)
            .ToListAsync(cancellationToken);

        var items = rows.Select(i => new BoardItem(
            i.Id, i.Ref, i.TypeId, i.Title, i.StatusId, i.Priority, i.AssigneeIds, i.DueDate, i.ParentId,
            i.Fields.Where(f => cardFields.GetValueOrDefault(i.TypeId)?.Contains(f.Key) == true).ToDictionary(f => f.Key, f => f.Value),
            i.Version)).ToList();

        return new BoardResponse(project.Id, boardWorkflows, items);
    }
}
