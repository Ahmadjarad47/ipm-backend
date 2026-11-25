using Ipm.Application.Abstractions.Data;
using Ipm.Domain.Approvals;
using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Approvals.Approvals.Common;

/// <summary>
/// R4: whether a request for this template and action makes sense for the subject right now. A project is approved once to start, closed while it
/// runs, and approved again only to reopen it; and while an approved request for the action is still unspent no second one is taken (the transition
/// spends it). Used by submitting and by the template lookup, so the screen never offers what the server would refuse.
/// </summary>
internal static class ApprovalEligibility
{
    public static async Task<Error?> CheckAsync(
        IApplicationDbContext db, ApprovalTemplate template, ApprovalSubject subject, ApprovalAction action, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (template.Event == ApprovalEvent.SensitiveChange)
        {
            return null;
        }

        if (subject.Item is null)
        {
            var category = await db.Statuses.AsNoTracking().Where(s => s.Id == subject.Project.StatusId).Select(s => s.Category).SingleAsync(cancellationToken);
            var running = category is StatusCategory.Active or StatusCategory.Blocked;
            if (template.Event == ApprovalEvent.ProjectClose && !running)
            {
                return ApprovalErrors.ProjectNotRunning;
            }

            if (template.Event == ApprovalEvent.ProjectApprove && running)
            {
                return ApprovalErrors.ProjectAlreadyRunning;
            }
        }

        var approved = await db.ApprovalRequests.AsNoTracking()
            .Where(r => r.SubjectType == subject.Type && r.SubjectId == subject.Id && r.TemplateId == template.Id && r.Status == ApprovalStatus.Approved && r.ConsumedAt == null)
            .ToListAsync(cancellationToken);
        return approved.Any(r => !r.IsExpired(now) && r.Action == action) ? ApprovalErrors.AlreadyApproved : null;
    }
}
