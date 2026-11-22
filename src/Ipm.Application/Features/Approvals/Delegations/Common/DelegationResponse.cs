namespace Ipm.Application.Features.Approvals.Delegations.Common;

/// <summary>A temporary hand-over of approval authority. The dates are inclusive, by the Riyadh calendar.</summary>
public sealed record DelegationResponse(Guid Id, Guid FromUserId, Guid ToUserId, DateOnly From, DateOnly To, string Reason, bool Active, DateTimeOffset CreatedAt);
