using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Features.Approvals.Approvals.Common;
using Ipm.Application.Features.Identity.Common;
using Ipm.Domain.Approvals;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Approvals.Approvals.Queries.GetApprovals;

internal sealed class GetApprovalsQueryHandler(IApplicationDbContext db, IUserContext currentUser, IAccessService access, TimeProvider time)
    : IQueryHandler<GetApprovalsQuery, PagedList<ApprovalResponse>>
{
    public async ValueTask<Result<PagedList<ApprovalResponse>>> Handle(GetApprovalsQuery query, CancellationToken cancellationToken)
    {
        var caller = await AccessChecks.CallerAsync(currentUser, access, cancellationToken);
        if (caller.IsFailure)
        {
            return caller.Error;
        }

        var (me, now) = (caller.Value.UserId, time.GetUtcNow());
        var requests = db.ApprovalRequests.AsNoTracking().AsQueryable();
        if (ApprovalParsing.TryParseStatus(query.Status, out var status))
        {
            requests = requests.Where(r => r.Status == status);
        }

        if (query.ProjectId is { } projectId)
        {
            requests = requests.Where(r => r.ProjectId == projectId);
        }

        if (ApprovalParsing.TryParseSubjectType(query.SubjectType, out var subjectType))
        {
            requests = requests.Where(r => r.SubjectType == subjectType);
        }

        if (query.SubjectId is { } subjectId)
        {
            requests = requests.Where(r => r.SubjectId == subjectId);
        }

        if (query.TemplateId is { } templateId)
        {
            requests = requests.Where(r => r.TemplateId == templateId);
        }

        // The decisions and approvers live in jsonb, so who may decide is worked out here, on the rows the simple filters kept.
        var all = await requests.ToListAsync(cancellationToken);
        var templates = await db.ApprovalTemplates.AsNoTracking().ToDictionaryAsync(t => t.Id, cancellationToken);
        var deciders = await ApprovalDeciders.LoadAsync(db, RiyadhClock.Today(time), cancellationToken);
        bool CanDecide(ApprovalRequest r) => deciders.CanDecide(r, me, templates.GetValueOrDefault(r.TemplateId));

        var box = query.Box ?? "inbox";
        var visible = all
            .Where(r => caller.Value.Can(Permissions.ProjectsView, r.ProjectId))
            .Where(r => r.RequesterId == me || CanDecide(r) || caller.Value.Can(Permissions.ApprovalsView, r.ProjectId))
            .Where(r => box switch
            {
                "inbox" => CanDecide(r),
                "sent" => r.RequesterId == me,
                _ => r.Status != ApprovalStatus.PendingApproval
                    && (r.RequesterId == me || r.Decisions.Exists(d => d.DeciderId == me) || caller.Value.Can(Permissions.ApprovalsView, r.ProjectId)),
            })
            .Where(r => !query.Overdue || r.IsOverdue(now))
            .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
            .ThenBy(r => r.Id)
            .ToList();

        var page = visible.Skip(query.Page.Skip).Take(query.Page.Take).Select(r => r.ToResponse(now, CanDecide(r))).ToList();
        return new PagedList<ApprovalResponse>(page, Math.Max(query.Page.Page, 1), query.Page.Take, visible.Count);
    }
}
