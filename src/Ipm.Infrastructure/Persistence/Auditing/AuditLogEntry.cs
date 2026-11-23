namespace Ipm.Infrastructure.Persistence.Auditing;

/// <summary>
/// One row of <c>public.audit_log</c>: who changed which record, when, and the values before and after.
/// Written by <see cref="AuditLogInterceptor"/> in the same transaction as the change itself.
/// Not a domain entity: it is never edited or deleted by the application.
/// </summary>
public sealed class AuditLogEntry
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// <summary>Table the record lives in, schema qualified (e.g. <c>people.employees</c>).</summary>
    public required string Entity { get; init; }

    public required Guid EntityId { get; init; }

    public required AuditAction Action { get; init; }

    /// <summary>JSON object of the values before the change (null for an insert).</summary>
    public string? Before { get; init; }

    /// <summary>JSON object of the values after the change (null for a delete).</summary>
    public string? After { get; init; }

    public Guid? UserId { get; init; }

    public DateTimeOffset At { get; init; }

    /// <summary>Position in the chain; set by the database trigger.</summary>
    public long Seq { get; init; }

    public Guid? ProjectId { get; init; }

    /// <summary>The work item the change belongs to (the item itself, or the one a comment or link hangs on).</summary>
    public Guid? WorkItemId { get; init; }

    /// <summary>Display name at the time of the change, so a later rename never rewrites history.</summary>
    public string? SubjectLabel { get; init; }

    /// <summary><c>ui</c>, <c>api</c>, <c>webhook</c> or <c>system</c>.</summary>
    public string Source { get; init; } = "system";

    public string CorrelationId { get; init; } = string.Empty;

    public string PrevHash { get; init; } = string.Empty;

    public string Hash { get; init; } = string.Empty;
}

public enum AuditAction
{
    Insert = 0,
    Update = 1,
    Delete = 2,
}
