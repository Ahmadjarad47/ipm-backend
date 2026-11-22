using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Identity.Users.Queries.GetUserDirectory;

internal sealed class GetUserDirectoryQueryHandler(IApplicationDbContext db)
    : IQueryHandler<GetUserDirectoryQuery, IReadOnlyList<UserDirectoryItem>>
{
    public async ValueTask<Result<IReadOnlyList<UserDirectoryItem>>> Handle(GetUserDirectoryQuery query, CancellationToken cancellationToken)
    {
        IReadOnlyList<UserDirectoryItem> items = await db.Users
            .AsNoTracking()
            .OrderBy(u => u.Name)
            .Select(u => new UserDirectoryItem(u.Id, u.Name, u.UserName!, u.Title, u.IsActive))
            .ToListAsync(cancellationToken);

        return Result.Success(items);
    }
}
