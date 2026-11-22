using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Approvals.Approvals.Common;
using Ipm.Domain.Common;

namespace Ipm.Application.Features.Approvals.Approvals.Queries.GetApprovalById;

internal sealed class GetApprovalByIdQueryHandler(IApplicationDbContext db, IUserContext currentUser, IAccessService access, TimeProvider time)
    : IQueryHandler<GetApprovalByIdQuery, ApprovalResponse>
{
    public async ValueTask<Result<ApprovalResponse>> Handle(GetApprovalByIdQuery query, CancellationToken cancellationToken)
    {
        var guarded = await ApprovalGuard.LoadAsync(db, currentUser, access, time, query.Id, track: false, cancellationToken);
        return guarded.IsFailure ? guarded.Error : guarded.Value.Request.ToResponse(time.GetUtcNow(), guarded.Value.CanDecide);
    }
}
