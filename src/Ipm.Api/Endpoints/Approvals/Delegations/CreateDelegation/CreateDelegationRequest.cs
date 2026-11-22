namespace Ipm.Api.Endpoints.Approvals.Delegations.CreateDelegation;

public sealed record CreateDelegationRequest(Guid? FromUserId, Guid ToUserId, DateOnly From, DateOnly To, string? Reason);
