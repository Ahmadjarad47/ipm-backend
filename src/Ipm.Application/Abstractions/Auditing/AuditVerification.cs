namespace Ipm.Application.Abstractions.Auditing;

/// <summary>R7: <see cref="BrokenAt"/> is the id of the first event whose chain does not match.</summary>
public sealed record AuditVerification(bool Ok, long Checked, Guid? BrokenAt);
