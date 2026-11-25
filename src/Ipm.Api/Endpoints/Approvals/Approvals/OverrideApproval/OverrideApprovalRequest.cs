using Ipm.Domain.Approvals;

namespace Ipm.Api.Endpoints.Approvals.Approvals.OverrideApproval;

public sealed record OverrideApprovalRequest(DecisionKind Decision, string? Comment, int Revision);
