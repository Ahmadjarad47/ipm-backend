using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Identity.Users.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Identity.Users.Queries.GetUserById;

internal sealed class GetUserByIdQueryHandler(IApplicationDbContext db, IUserContext currentUser, IAccessService access)
    : IQueryHandler<GetUserByIdQuery, UserResponse>
{
    public async ValueTask<Result<UserResponse>> Handle(GetUserByIdQuery query, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == query.Id, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        // R20: only user administrators see e-mail, manager and roles.
        var canAdminister = currentUser.UserId is { } id && await access.CanAsync(id, Permissions.PermissionsAssign, cancellationToken: cancellationToken);
        return canAdminister ? user.ToResponse() : user.ToDirectoryResponse();
    }
}
