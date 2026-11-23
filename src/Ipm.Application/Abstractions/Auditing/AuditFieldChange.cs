namespace Ipm.Application.Abstractions.Auditing;

/// <summary>One changed column of an audit event.</summary>
public sealed record AuditFieldChange(string Field, string? From, string? To);
