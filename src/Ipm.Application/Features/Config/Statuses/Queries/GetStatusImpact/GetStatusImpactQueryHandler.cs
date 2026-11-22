using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.WorkItemTypes.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Config.Statuses.Queries.GetStatusImpact;

internal sealed class GetStatusImpactQueryHandler(IApplicationDbContext db) : IQueryHandler<GetStatusImpactQuery, ImpactResponse>
{
    public async ValueTask<Result<ImpactResponse>> Handle(GetStatusImpactQuery query, CancellationToken cancellationToken)
    {
        var status = await db.Statuses.AsNoTracking().SingleOrDefaultAsync(s => s.Id == query.Id, cancellationToken);
        if (status is null)
        {
            return StatusErrors.NotFound;
        }

        var items = await db.WorkItems.CountAsync(i => i.StatusId == status.Id, cancellationToken);
        var projects = status.Scope == StatusScope.Project
            ? await db.Projects.CountAsync(p => p.StatusId == status.Id, cancellationToken)
            : await db.WorkItems.Where(i => i.StatusId == status.Id).Select(i => i.ProjectId).Distinct().CountAsync(cancellationToken);

        var workflows = await db.Workflows.AsNoTracking().Select(w => new { w.Id, w.Name, w.StatusIds, w.InitialStatusId }).ToListAsync(cancellationToken);
        return new ImpactResponse(
            items,
            projects,
            workflows.Count(w => w.StatusIds.Contains(status.Id)),
            [.. workflows.Where(w => w.InitialStatusId == status.Id).Select(w => new ImpactRef(w.Id, w.Name))]);
    }
}
