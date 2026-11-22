using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Config.WorkItemTypes.Queries.GetHierarchyRules;

internal sealed class GetHierarchyRulesQueryHandler(IApplicationDbContext db) : IQueryHandler<GetHierarchyRulesQuery, HierarchyRulesResponse>
{
    public async ValueTask<Result<HierarchyRulesResponse>> Handle(GetHierarchyRulesQuery query, CancellationToken cancellationToken)
    {
        var types = await db.WorkItemTypes
            .AsNoTracking()
            .OrderBy(t => t.SortOrder).ThenBy(t => t.Name)
            .Select(t => new { t.Id, t.AllowedChildTypeIds })
            .ToListAsync(cancellationToken);

        var pairs = types.SelectMany(t => t.AllowedChildTypeIds.Select(child => new HierarchyPair(t.Id, child))).ToList();
        return new HierarchyRulesResponse(pairs);
    }
}
