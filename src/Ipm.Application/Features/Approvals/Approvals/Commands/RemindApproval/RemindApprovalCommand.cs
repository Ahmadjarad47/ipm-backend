using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Approvals.Approvals.Common;

namespace Ipm.Application.Features.Approvals.Approvals.Commands.RemindApproval;

/// <summary>Reminds the approvers of the active stage (R14): its requester or a holder of approvals.configure, once an hour.</summary>
public sealed record RemindApprovalCommand(Guid Id) : ICommand<ApprovalResponse>;
