using Ipm.Domain.Approvals;

namespace Ipm.Application.Features.Approvals.Delegations.Common;

internal static class DelegationMapping
{
    public static DelegationResponse ToResponse(this Delegation d) => new(d.Id, d.FromUserId, d.ToUserId, d.FromDate, d.ToDate, d.Reason, d.IsActive, d.CreatedAt);
}
