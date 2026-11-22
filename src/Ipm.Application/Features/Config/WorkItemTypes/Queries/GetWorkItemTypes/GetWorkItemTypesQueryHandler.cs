using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.WorkItemTypes.Common;
using Ipm.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Config.WorkItemTypes.Queries.GetWorkItemTypes;

internal sealed class GetWorkItemTypesQueryHandler(IApplicationDbContext db)
    : IQueryHandler<GetWorkItemTypesQuery, IReadOnlyList<WorkItemTypeResponse>>
{
    public async ValueTask<Result<IReadOnlyList<WorkItemTypeResponse>>> Handle(GetWorkItemTypesQuery query, CancellationToken cancellationToken)
    {
        var types = db.WorkItemTypes.AsNoTracking();
        if (query.Active is { } active)
        {
            types = types.Where(t => t.IsActive == active);
        }

        var rows = await types.OrderBy(t => t.SortOrder).ThenBy(t => t.Name).ToListAsync(cancellationToken);
        IReadOnlyList<WorkItemTypeResponse> items = [.. rows.Select(t => t.ToResponse())];
        return Result.Success(items);
    }
}
