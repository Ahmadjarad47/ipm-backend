using Ipm.Domain.Approvals;

namespace Ipm.Application.Features.Approvals.Approvals.Common;

/// <summary>
/// One approval request. <see cref="Version"/> is sent as the ETag. <see cref="Overdue"/> and <see cref="CanDecide"/> are computed for the caller:
/// the active stage is past its due time, and the caller may decide it right now.
/// </summary>
public sealed record ApprovalResponse(
    Guid Id,
    Guid TemplateId,
    ApprovalSubjectType SubjectType,
    Guid SubjectId,
    Guid ProjectId,
    string Title,
    Guid RequesterId,
    ApprovalStatus Status,
    int CurrentStage,
    int Revision,
    IReadOnlyDictionary<string, string?> Snapshot,
    IReadOnlyList<ApprovalDecision> Decisions,
    IReadOnlyList<ApprovalStage> Stages,
    ApprovalAction? Action,
    Guid? TargetStatusId,
    DateTimeOffset? StageDueAt,
    DateTimeOffset? LastRemindedAt,
    DateTimeOffset? ConsumedAt,
    bool Overdue,
    bool CanDecide,
    uint Version);
