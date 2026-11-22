using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.WorkItemTypes.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Config.WorkItemTypes.Queries.GetWorkItemTypeById;

internal sealed class GetWorkItemTypeByIdQueryHandler(IApplicationDbContext db) : IQueryHandler<GetWorkItemTypeByIdQuery, WorkItemTypeResponse>
{
    public async ValueTask<Result<WorkItemTypeResponse>> Handle(GetWorkItemTypeByIdQuery query, CancellationToken cancellationToken)
    {
        var type = await db.WorkItemTypes.AsNoTracking().SingleOrDefaultAsync(t => t.Id == query.Id, cancellationToken);
        return type is null ? WorkItemTypeErrors.NotFound : type.ToResponse();
    }
}
