using Ipm.Application.Abstractions.Data;
using Ipm.Domain.Approvals;
using Ipm.Domain.Config;
using Ipm.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Approvals.Approvals.Common;

/// <summary>
/// The final decision on a project approval moves the project by itself: approved → the active status, rejected → the cancelled status.
/// Only a project still waiting to start (not-started category) is moved, so a late decision never overrides a later manual change.
/// The approval is spent by the move (single use), exactly as a manual transition would spend it. Applied in the caller's transaction.
/// </summary>
internal static class ProjectApprovalOutcome
{
    public static async Task ApplyAsync(
        IApplicationDbContext db, ApprovalRequest request, ApprovalTemplate? template, Guid actorId, string? comment, bool overridden, TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (request.SubjectType != ApprovalSubjectType.Project || template?.Event != ApprovalEvent.ProjectApprove || request.Action is not (null or ApprovalAction.Activate))
        {
            return;
        }

        var approved = request.Status == ApprovalStatus.Approved;
        if (!approved && request.Status != ApprovalStatus.Rejected)
        {
            return; // still in progress, or sent back for changes: the project stays as it is
        }

        var project = await db.Projects.SingleOrDefaultAsync(p => p.Id == request.SubjectId, cancellationToken);
        if (project is null)
        {
            return;
        }

        var statuses = await db.Statuses.AsNoTracking().Where(s => s.Scope == StatusScope.Project && s.IsActive).OrderBy(s => s.SortOrder).ToListAsync(cancellationToken);
        var from = statuses.Find(s => s.Id == project.StatusId);
        if (from?.Category != StatusCategory.NotStarted)
        {
            return;
        }

        var wanted = approved ? StatusCategory.Active : StatusCategory.Cancelled;
        var to = (approved ? statuses.Find(s => s.Id == request.TargetStatusId && s.Category == wanted) : null) ?? statuses.Find(s => s.Category == wanted);
        if (to is null)
        {
            return;
        }

        var now = time.GetUtcNow();
        project.MoveTo(to.Id);
        db.ProjectStatusChanges.Add(ProjectStatusChange.Create(project.Id, from.Id, to.Id, actorId, comment, now, overridden));
        if (approved)
        {
            request.Consume(now);
        }
    }
}
