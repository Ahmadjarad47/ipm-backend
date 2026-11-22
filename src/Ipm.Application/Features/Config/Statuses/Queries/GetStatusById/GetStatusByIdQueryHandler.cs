using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.Statuses.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Config.Statuses.Queries.GetStatusById;

internal sealed class GetStatusByIdQueryHandler(IApplicationDbContext db) : IQueryHandler<GetStatusByIdQuery, StatusResponse>
{
    public async ValueTask<Result<StatusResponse>> Handle(GetStatusByIdQuery query, CancellationToken cancellationToken)
    {
        var status = await db.Statuses.AsNoTracking().SingleOrDefaultAsync(s => s.Id == query.Id, cancellationToken);
        return status is null ? StatusErrors.NotFound : status.ToResponse();
    }
}
