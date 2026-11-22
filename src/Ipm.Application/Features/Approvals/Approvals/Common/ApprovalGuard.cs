using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Common;
using Ipm.Application.Features.Identity.Common;
using Ipm.Domain.Approvals;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Ipm.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Approvals.Approvals.Common;


/// <summary>
/// R12: a request is hidden (404) from anyone who cannot see its project, and inside it from anyone who is neither its requester,
/// nor able to decide it, nor holder of approvals.view there. What the caller may then do is checked by the use case (403).
/// </summary>
internal static class ApprovalGuard
{
    public static async Task<Result<GuardedApproval>> LoadAsync(
        IApplicationDbContext db, IUserContext user, IAccessService access, TimeProvider time, Guid id, bool track, CancellationToken cancellationToken)
    {
        var requests = track ? db.ApprovalRequests.AsQueryable() : db.ApprovalRequests.AsNoTracking();
        var request = await requests.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (request is null)
        {
            return ApprovalErrors.NotFound;
        }

        var caller = await AccessChecks.CallerAsync(user, access, cancellationToken);
        if (caller.IsFailure)
        {
            return caller.Error;
        }

        if (!caller.Value.Can(Permissions.ProjectsView, request.ProjectId))
        {
            return ApprovalErrors.NotFound;
        }

        var project = await db.Projects.AsNoTracking().Include(p => p.Members).SingleAsync(p => p.Id == request.ProjectId, cancellationToken);
        var template = await db.ApprovalTemplates.AsNoTracking().SingleOrDefaultAsync(t => t.Id == request.TemplateId, cancellationToken);
        var deciders = await ApprovalDeciders.LoadAsync(db, RiyadhClock.Today(time), cancellationToken);
        var guarded = new GuardedApproval(request, template, project, caller.Value, deciders);

        var visible = request.RequesterId == caller.Value.UserId || guarded.CanDecide || caller.Value.Can(Permissions.ApprovalsView, request.ProjectId);
        return visible ? guarded : ApprovalErrors.NotFound;
    }
}
