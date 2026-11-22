using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Approvals.Delegations.Common;
using Ipm.Application.Features.Identity.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Approvals.Delegations.Queries.GetDelegations;

internal sealed class GetDelegationsQueryHandler(IApplicationDbContext db, IUserContext currentUser, IAccessService access)
    : IQueryHandler<GetDelegationsQuery, IReadOnlyList<DelegationResponse>>
{
    public async ValueTask<Result<IReadOnlyList<DelegationResponse>>> Handle(GetDelegationsQuery query, CancellationToken cancellationToken)
    {
        var caller = await AccessChecks.CallerAsync(currentUser, access, cancellationToken);
        if (caller.IsFailure)
        {
            return caller.Error;
        }

        var all = caller.Value.Has(Permissions.ApprovalsConfigure);
        var me = caller.Value.UserId;
        var rows = await db.Delegations.AsNoTracking()
            .Where(d => all || d.FromUserId == me || d.ToUserId == me)
            .OrderByDescending(d => d.CreatedAt).ThenBy(d => d.Id)
            .ToListAsync(cancellationToken);
        return rows.Select(d => d.ToResponse()).ToList();
    }
}
