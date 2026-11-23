using Ipm.Application.Abstractions.Data;
using Ipm.Domain.Approvals;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Approvals.Approvals.Common;

/// <summary>R6 and R13: who may decide the active stage right now, with the delegations in effect today.</summary>
internal sealed class ApprovalDeciders(IReadOnlyList<Delegation> inEffect, bool globalSelfApproval)
{
    public static async Task<ApprovalDeciders> LoadAsync(IApplicationDbContext db, DateOnly today, CancellationToken cancellationToken) =>
        new(
            await db.Delegations.AsNoTracking().Where(d => d.IsActive && d.FromDate <= today && today <= d.ToDate).ToListAsync(cancellationToken),
            await db.Settings.AsNoTracking().Select(s => s.AllowSelfApproval).SingleOrDefaultAsync(cancellationToken));

    /// <summary>R6: the self-decision exception needs the global setting (part 09, off until then) and the template flag, so for now it never applies.</summary>
    public bool SelfDecisionAllowed(ApprovalTemplate? template) => globalSelfApproval && template?.AllowSelfApproval == true;

    /// <summary>The user the caller acts for in the active stage, when they are a delegate and not a direct approver.</summary>
    public Guid? OnBehalfOf(ApprovalRequest request, Guid userId)
    {
        var direct = request.DirectApproversOf(request.CurrentStage);
        return direct.Contains(userId) ? null : inEffect.FirstOrDefault(d => d.ToUserId == userId && direct.Contains(d.FromUserId))?.FromUserId;
    }

    /// <summary>True when the caller is a direct approver of the active stage or a delegate of one.</summary>
    public bool IsApprover(ApprovalRequest request, Guid userId)
    {
        var direct = request.DirectApproversOf(request.CurrentStage);
        return direct.Contains(userId) || inEffect.Any(d => d.ToUserId == userId && direct.Contains(d.FromUserId));
    }

    public bool CanDecide(ApprovalRequest request, Guid userId, ApprovalTemplate? template) =>
        request.Status == ApprovalStatus.PendingApproval
        && (request.RequesterId != userId || SelfDecisionAllowed(template))
        && IsApprover(request, userId);
}
