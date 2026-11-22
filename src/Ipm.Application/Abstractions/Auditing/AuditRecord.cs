using System.Text.Json;

namespace Ipm.Application.Abstractions.Auditing;

/// <summary>One change of one record, as read from <c>audit_log</c>: who, when, and the values before and after.</summary>
public sealed record AuditRecord(
    DateTimeOffset At,
    Guid? UserId,
    string? UserName,
    string Action,
    JsonElement? Before,
    JsonElement? After);
