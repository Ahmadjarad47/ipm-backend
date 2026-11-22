using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.RelationTypes.Common;
using Ipm.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Config.RelationTypes.Queries.GetRelationTypes;

internal sealed class GetRelationTypesQueryHandler(IApplicationDbContext db)
    : IQueryHandler<GetRelationTypesQuery, IReadOnlyList<RelationTypeResponse>>
{
    public async ValueTask<Result<IReadOnlyList<RelationTypeResponse>>> Handle(GetRelationTypesQuery query, CancellationToken cancellationToken)
    {
        var types = db.RelationTypes.AsNoTracking();
        if (query.Active is { } active)
        {
            types = types.Where(t => t.IsActive == active);
        }

        var rows = await types.OrderBy(t => t.Name).ToListAsync(cancellationToken);
        IReadOnlyList<RelationTypeResponse> items = [.. rows.Select(t => t.ToResponse())];
        return Result.Success(items);
    }
}
