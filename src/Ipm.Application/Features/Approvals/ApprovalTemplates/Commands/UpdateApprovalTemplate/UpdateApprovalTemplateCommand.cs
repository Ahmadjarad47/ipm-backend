using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Approvals.ApprovalTemplates.Common;
using Ipm.Domain.Approvals;

namespace Ipm.Application.Features.Approvals.ApprovalTemplates.Commands.UpdateApprovalTemplate;

/// <summary>Replaces the definition of a template. A running request keeps its own copy of the stages (R1). The event cannot change once requests exist.</summary>
public sealed record UpdateApprovalTemplateCommand(
    Guid Id,
    string Name,
    ApprovalEvent Event,
    bool? Active,
    IReadOnlyList<ApprovalStage> Stages,
    IReadOnlyList<string> ProtectedFields,
    ApprovalRule? Rule,
    bool AllowSelfApproval,
    uint ExpectedVersion) : ICommand<ApprovalTemplateResponse>;
