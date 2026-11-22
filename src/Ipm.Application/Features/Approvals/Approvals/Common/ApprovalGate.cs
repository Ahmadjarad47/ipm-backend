using Ipm.Application.Abstractions.Data;
using Ipm.Domain.Approvals;
using Ipm.Domain.Config;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Approvals.Approvals.Common;

/// <summary>R9 and R10: the gate on moves marked requiresApproval.</summary>
internal static class ApprovalGate
{
    /// <summary>What a project move is: reaching a done status closes it, any other move activates it.</summary>
    public static ApprovalAction ProjectAction(StatusCategory target) => target == StatusCategory.Done ? ApprovalAction.Close : ApprovalAction.Activate;

    /// <summary>What a work item move is: leaving a done or cancelled status reopens it, any other move closes it.</summary>
    public static ApprovalAction ItemAction(StatusCategory current) =>
        current is StatusCategory.Done or StatusCategory.Cancelled ? ApprovalAction.Reopen : ApprovalAction.Close;

    /// <summary>
    /// An approved, unspent, unexpired request of the right event whose action and target status match the move and whose protected
    /// fields still equal the snapshot (newest first). The caller spends it with <see cref="ApprovalRequest.Consume"/> in the same save.
    /// </summary>
    public static async Task<GateResult> FindAsync(
        IApplicationDbContext db, ApprovalSubject subject, ApprovalAction action, Guid targetStatusId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var requests = await db.ApprovalRequests
            .Where(r => r.SubjectType == subject.Type && r.SubjectId == subject.Id && r.Status == ApprovalStatus.Approved && r.ConsumedAt == null)
            .ToListAsync(cancellationToken);
        var events = await db.ApprovalTemplates.AsNoTracking().Select(t => new { t.Id, t.Event }).ToDictionaryAsync(t => t.Id, t => t.Event, cancellationToken);
        var wantedEvent = ApprovalSubjectFields.EventFor(subject.Type, action);

        var candidates = requests
            .Where(r => events.GetValueOrDefault(r.TemplateId) == wantedEvent && !r.IsExpired(now)
                && (r.Action is null || r.Action == action) && (r.TargetStatusId is null || r.TargetStatusId == targetStatusId))
            .OrderByDescending(r => r.ApprovedAt)
            .ToList();
        foreach (var candidate in candidates)
        {
            if ((await ApprovalSubjects.ChangedSinceAsync(db, subject, candidate.Snapshot, cancellationToken)).Count == 0)
            {
                return new GateResult(candidate, false);
            }
        }

        return new GateResult(null, candidates.Count > 0);
    }
}
