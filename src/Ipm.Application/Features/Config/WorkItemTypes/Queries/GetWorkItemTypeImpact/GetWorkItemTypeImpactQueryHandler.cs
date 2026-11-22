using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.WorkItemTypes.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Config.WorkItemTypes.Queries.GetWorkItemTypeImpact;

internal sealed class GetWorkItemTypeImpactQueryHandler(IApplicationDbContext db) : IQueryHandler<GetWorkItemTypeImpactQuery, ImpactResponse>
{
    public async ValueTask<Result<ImpactResponse>> Handle(GetWorkItemTypeImpactQuery query, CancellationToken cancellationToken)
    {
        var type = await db.WorkItemTypes.AsNoTracking().SingleOrDefaultAsync(t => t.Id == query.Id, cancellationToken);
        if (type is null)
        {
            return WorkItemTypeErrors.NotFound;
        }

        var items = await db.WorkItems.CountAsync(i => i.TypeId == type.Id, cancellationToken);
        var projects = await db.Projects.AsNoTracking().Select(p => new { p.Id, p.Code, p.Name, p.AllowedTypeIds, p.DefaultTypeId, p.WorkflowOverrides }).ToListAsync(cancellationToken);
        var allowing = projects.Where(p => p.AllowedTypeIds.Contains(type.Id)).ToList();

        var workflowIds = new HashSet<Guid>();
        if (type.WorkflowId is { } own)
        {
            workflowIds.Add(own);
        }

        foreach (var overrideOf in allowing.SelectMany(p => p.WorkflowOverrides).Where(o => o.TypeId == type.Id))
        {
            workflowIds.Add(overrideOf.WorkflowId);
        }

        return new ImpactResponse(
            items,
            allowing.Count,
            workflowIds.Count,
            [.. projects.Where(p => p.DefaultTypeId == type.Id).Select(p => new ImpactRef(p.Id, p.Name))]);
    }
}
