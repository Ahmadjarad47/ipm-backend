using Ipm.Domain.Approvals;

namespace Ipm.Application.Features.Approvals.ApprovalTemplates.Common;

/// <summary>An approval template. <see cref="Version"/> is sent as the ETag.</summary>
public sealed record ApprovalTemplateResponse(
    Guid Id,
    string Name,
    ApprovalEvent Event,
    bool Active,
    IReadOnlyList<ApprovalStage> Stages,
    IReadOnlyList<string> ProtectedFields,
    ApprovalRule? Rule,
    bool AllowSelfApproval,
    uint Version);
