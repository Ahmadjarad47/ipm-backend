using Ipm.Application.Abstractions.Auditing;
using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Activity.Common;

/// <summary>Spec 08 R2 and R4: what the caller may see of the audit log, and how a day filter becomes an instant.</summary>
internal static class AuditAccess
{
    /// <summary>Everything for <c>audit.view</c>; otherwise only the projects the caller can see (the security events and the events without a project stay hidden).</summary>
    public static async Task<AuditVisibility> VisibilityAsync(IApplicationDbContext db, UserAccess caller, CancellationToken cancellationToken)
    {
        if (caller.Has(Permissions.AuditView))
        {
            return AuditVisibility.Everything;
        }

        if (caller.Has(Permissions.ProjectsView))
        {
            return AuditVisibility.OfProjects(await db.Projects.AsNoTracking().Select(p => p.Id).ToListAsync(cancellationToken));
        }

        return caller.Denied.Contains(Permissions.ProjectsView)
            ? AuditVisibility.OfProjects([])
            : AuditVisibility.OfProjects(caller.ProjectPermissions.Where(p => p.Value.Contains(Permissions.ProjectsView)).Select(p => p.Key));
    }

    private static readonly TimeSpan Riyadh = TimeSpan.FromHours(3);

    public static DateTimeOffset? StartOf(DateOnly? day) => day is { } d ? new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue), Riyadh).ToUniversalTime() : null;

    /// <summary>The instant after the end of the day, so a filter <c>to</c> includes that whole day.</summary>
    public static DateTimeOffset? EndOf(DateOnly? day) => day is { } d ? new DateTimeOffset(d.AddDays(1).ToDateTime(TimeOnly.MinValue), Riyadh).ToUniversalTime() : null;

    public static readonly string[] Sources = ["ui", "api", "webhook", "system"];
}
