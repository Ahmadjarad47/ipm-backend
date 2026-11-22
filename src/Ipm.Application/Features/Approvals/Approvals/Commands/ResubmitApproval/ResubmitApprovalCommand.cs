using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Approvals.Approvals.Common;

namespace Ipm.Application.Features.Approvals.Approvals.Commands.ResubmitApproval;

/// <summary>Sends a returned or superseded request again: a new revision from stage 1 (R7). Only its requester.</summary>
public sealed record ResubmitApprovalCommand(Guid Id, uint ExpectedVersion) : ICommand<ApprovalResponse>;
