namespace Ipm.Application.Abstractions.Auditing;

/// <summary>R2: what the caller may see. <see cref="All"/> (audit.view) sees everything including security events; otherwise only events of these projects.</summary>
public sealed record AuditVisibility(bool All, IReadOnlyCollection<Guid> ProjectIds)
{
    public static AuditVisibility Everything { get; } = new(true, []);

    public static AuditVisibility OfProjects(IEnumerable<Guid> projectIds) => new(false, [.. projectIds]);
}
