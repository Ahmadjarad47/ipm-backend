using Ipm.Domain.Approvals;

namespace Ipm.Api.Endpoints.Approvals.ApprovalTemplates.UpdateApprovalTemplate;

public sealed record UpdateApprovalTemplateRequest(
    string Name,
    ApprovalEvent Event,
    bool? Active,
    IReadOnlyList<ApprovalStage>? Stages,
    IReadOnlyList<string>? ProtectedFields,
    ApprovalRule? Rule,
    bool? AllowSelfApproval);
