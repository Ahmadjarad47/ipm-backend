using Ipm.Domain.Approvals;

namespace Ipm.Api.Endpoints.Approvals.ApprovalTemplates.CreateApprovalTemplate;

public sealed record CreateApprovalTemplateRequest(
    string Name,
    ApprovalEvent Event,
    bool? Active,
    IReadOnlyList<ApprovalStage>? Stages,
    IReadOnlyList<string>? ProtectedFields,
    ApprovalRule? Rule,
    bool? AllowSelfApproval);
