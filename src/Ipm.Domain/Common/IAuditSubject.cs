namespace Ipm.Domain.Common;

/// <summary>
/// Part 08: what the audit log needs to know about a changed record to file it under a project and under a work item. Entities that do not
/// implement it (users, roles, types, statuses…) are logged without a project, so only holders of <c>audit.view</c> see their events.
/// </summary>
public interface IAuditSubject
{
    /// <summary>The project the change belongs to (a project is its own).</summary>
    Guid? AuditProjectId => null;

    /// <summary>The work item the change belongs to (the item itself, or the one a comment or link hangs on).</summary>
    Guid? AuditWorkItemId => null;

    /// <summary>The display name at the time of the change; null lets the log take the work item ref.</summary>
    string? AuditLabel => null;
}
