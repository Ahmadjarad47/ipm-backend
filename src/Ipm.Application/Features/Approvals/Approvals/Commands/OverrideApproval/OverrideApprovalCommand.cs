using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Approvals.Approvals.Common;
using Ipm.Domain.Approvals;

namespace Ipm.Application.Features.Approvals.Approvals.Commands.OverrideApproval;

/// <summary>A final decision by a holder of <c>audit.override</c> that skips the remaining stages. The comment (the reason) is mandatory.</summary>
public sealed record OverrideApprovalCommand(Guid Id, DecisionKind Decision, string? Comment, int Revision, uint ExpectedVersion) : ICommand<ApprovalResponse>;
