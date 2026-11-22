using Ipm.Application.Abstractions.Data;
using Ipm.Domain.Approvals;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Approvals.Approvals.Common;

/// <summary>R11: a change of a protected field supersedes the pending requests that protect it. Called before the save, so it is one transaction with the change.</summary>
internal static class ApprovalSupersession
{
    public static async Task SupersedeAsync(
        IApplicationDbContext db, ApprovalSubjectType type, Guid subjectId, IReadOnlyCollection<string> changedFields, CancellationToken cancellationToken)
    {
        if (changedFields.Count == 0)
        {
            return;
        }

        var pending = await db.ApprovalRequests
            .Where(r => r.SubjectType == type && r.SubjectId == subjectId && r.Status == ApprovalStatus.PendingApproval)
            .ToListAsync(cancellationToken);
        if (pending.Count == 0)
        {
            return;
        }

        var templateIds = pending.Select(r => r.TemplateId).Distinct().ToList();
        var templates = await db.ApprovalTemplates.AsNoTracking().Where(t => templateIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, cancellationToken);
        foreach (var request in pending.Where(r => templates.TryGetValue(r.TemplateId, out var t) && t.ProtectedFields.Exists(changedFields.Contains)))
        {
            request.Supersede();
        }
    }
}
