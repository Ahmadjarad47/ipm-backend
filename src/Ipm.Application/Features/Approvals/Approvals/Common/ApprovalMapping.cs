using Ipm.Domain.Approvals;

namespace Ipm.Application.Features.Approvals.Approvals.Common;

internal static class ApprovalMapping
{
    public static ApprovalResponse ToResponse(this ApprovalRequest request, DateTimeOffset now, bool canDecide) => new(
        request.Id,
        request.TemplateId,
        request.SubjectType,
        request.SubjectId,
        request.ProjectId,
        request.Title,
        request.RequesterId,
        request.Status,
        request.CurrentStage,
        request.Revision,
        request.Snapshot.ToDictionary(s => s.Field, s => s.Value),
        request.ProposedChanges.ToDictionary(s => s.Field, s => s.Value),
        request.Decisions,
        request.Stages,
        request.Action,
        request.TargetStatusId,
        request.StageDueAt,
        request.LastRemindedAt,
        request.ConsumedAt,
        request.IsOverdue(now),
        canDecide,
        request.Version);
}
