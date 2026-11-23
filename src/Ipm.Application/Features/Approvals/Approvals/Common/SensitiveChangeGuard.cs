using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Domain.Approvals;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Approvals.Approvals.Common;

/// <summary>06b R8 and R9: a direct edit of a field that an active sensitive-change template protects for this subject is refused.</summary>
internal static class SensitiveChangeGuard
{
    /// <summary>
    /// Fails with <see cref="ApprovalErrors.ChangeRequiresApproval"/> when an active sensitive-change template governs the subject (its rule,
    /// for the action change) and protects one of <paramref name="changedFields"/> (fields whose value really changes). A holder of audit.override may edit directly.
    /// </summary>
    public static async Task<Result> EnsureAsync(
        IApplicationDbContext db, UserAccess caller, ApprovalSubjectType type, Guid subjectId, IReadOnlyCollection<string> changedFields, CancellationToken cancellationToken)
    {
        if (changedFields.Count == 0 || caller.Has(Permissions.AuditOverride))
        {
            return Result.Success();
        }

        var templates = await db.ApprovalTemplates.AsNoTracking().Where(t => t.IsActive && t.Event == ApprovalEvent.SensitiveChange).ToListAsync(cancellationToken);
        if (templates.Count == 0)
        {
            return Result.Success();
        }

        var subject = await ApprovalSubjects.LoadAsync(db, type, subjectId, cancellationToken);
        if (subject is null)
        {
            return Result.Success();
        }

        var governing = templates
            .Where(t => TemplateMatcher.Matches(t, subject, ApprovalAction.Change) && t.ProtectedFields.Exists(changedFields.Contains))
            .OrderByDescending(t => t.Rule?.Priority ?? 0)
            .ThenBy(t => t.CreatedAt)
            .FirstOrDefault();
        return governing is null
            ? Result.Success()
            : ApprovalErrors.ChangeRequiresApproval(governing.Name, changedFields.Where(governing.ProtectedFields.Contains));
    }
}
