using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Approvals.Approvals.Common;
using Ipm.Domain.Approvals;

namespace Ipm.Application.Features.Approvals.Approvals.Commands.DecideApproval;

/// <summary>Decides the active stage (R6). <paramref name="Revision"/> is the revision the decider reviewed.</summary>
public sealed record DecideApprovalCommand(Guid Id, DecisionKind Decision, string? Comment, int Revision, uint ExpectedVersion) : ICommand<ApprovalResponse>;
