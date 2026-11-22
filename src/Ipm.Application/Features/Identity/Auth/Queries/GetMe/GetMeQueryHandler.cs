using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Identity.Auth.Queries.GetMe;

internal sealed class GetMeQueryHandler(IApplicationDbContext db, IUserContext currentUser, IAccessService access)
    : IQueryHandler<GetMeQuery, MeResponse>
{
    public async ValueTask<Result<MeResponse>> Handle(GetMeQuery query, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == currentUser.UserId, cancellationToken);
        var current = currentUser.UserId is { } id ? await access.GetAsync(id, cancellationToken) : null;
        if (user is null || current is null)
        {
            return UserErrors.NotFound;
        }

        return new MeResponse(
            new MeUser(user.Id, user.Name, user.UserName!, user.Email!, user.Title),
            current.Effective,
            current.ProjectPermissions.ToDictionary(p => p.Key.ToString(), p => p.Value),
            current.Denied,
            user.MustChangePassword);
    }
}
