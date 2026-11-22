using Ipm.Domain.Approvals;

namespace Ipm.Application.Features.Approvals.Approvals.Common;

/// <summary>The outcome of looking for the approval a gated move needs: the approval to spend, or whether protected fields drifted since it was given.</summary>
internal sealed record GateResult(ApprovalRequest? Request, bool Drifted);
