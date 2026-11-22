using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.Statuses.Common;
using Ipm.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Config.Statuses.Queries.GetStatuses;

internal sealed class GetStatusesQueryHandler(IApplicationDbContext db) : IQueryHandler<GetStatusesQuery, IReadOnlyList<StatusResponse>>
{
    public async ValueTask<Result<IReadOnlyList<StatusResponse>>> Handle(GetStatusesQuery query, CancellationToken cancellationToken)
    {
        var statuses = db.Statuses.AsNoTracking();
        if (query.Scope is { } scope)
        {
            statuses = statuses.Where(s => s.Scope == scope);
        }

        if (query.Active is { } active)
        {
            statuses = statuses.Where(s => s.IsActive == active);
        }

        var rows = await statuses.OrderBy(s => s.Scope).ThenBy(s => s.SortOrder).ThenBy(s => s.Name).ToListAsync(cancellationToken);
        IReadOnlyList<StatusResponse> items = [.. rows.Select(s => s.ToResponse())];
        return Result.Success(items);
    }
}
