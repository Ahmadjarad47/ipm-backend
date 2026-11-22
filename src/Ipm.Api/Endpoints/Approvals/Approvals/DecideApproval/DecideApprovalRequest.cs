using Ipm.Domain.Approvals;

namespace Ipm.Api.Endpoints.Approvals.Approvals.DecideApproval;

public sealed record DecideApprovalRequest(DecisionKind Decision, string? Comment, int Revision);
