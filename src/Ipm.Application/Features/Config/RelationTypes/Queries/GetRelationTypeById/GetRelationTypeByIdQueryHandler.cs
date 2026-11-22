using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.RelationTypes.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Config.RelationTypes.Queries.GetRelationTypeById;

internal sealed class GetRelationTypeByIdQueryHandler(IApplicationDbContext db) : IQueryHandler<GetRelationTypeByIdQuery, RelationTypeResponse>
{
    public async ValueTask<Result<RelationTypeResponse>> Handle(GetRelationTypeByIdQuery query, CancellationToken cancellationToken)
    {
        var type = await db.RelationTypes.AsNoTracking().SingleOrDefaultAsync(t => t.Id == query.Id, cancellationToken);
        return type is null ? RelationTypeErrors.NotFound : type.ToResponse();
    }
}
