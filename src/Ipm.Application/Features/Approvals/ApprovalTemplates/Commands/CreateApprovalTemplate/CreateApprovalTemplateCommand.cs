using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Approvals.ApprovalTemplates.Common;
using Ipm.Domain.Approvals;

namespace Ipm.Application.Features.Approvals.ApprovalTemplates.Commands.CreateApprovalTemplate;

/// <summary>Creates an approval template (R1, R2).</summary>
public sealed record CreateApprovalTemplateCommand(
    string Name,
    ApprovalEvent Event,
    bool? Active,
    IReadOnlyList<ApprovalStage> Stages,
    IReadOnlyList<string> ProtectedFields,
    ApprovalRule? Rule,
    bool AllowSelfApproval) : ICommand<ApprovalTemplateResponse>;
