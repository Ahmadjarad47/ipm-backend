using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Identity.Roles.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Identity.Roles.Queries.GetRoleById;

internal sealed class GetRoleByIdQueryHandler(IApplicationDbContext db) : IQueryHandler<GetRoleByIdQuery, RoleResponse>
{
    public async ValueTask<Result<RoleResponse>> Handle(GetRoleByIdQuery query, CancellationToken cancellationToken)
    {
        var role = await db.Roles.AsNoTracking().SingleOrDefaultAsync(r => r.Id == query.Id, cancellationToken);
        return role is null ? RoleErrors.NotFound : role.ToResponse();
    }
}
