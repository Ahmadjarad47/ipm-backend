using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ipm.Infrastructure.Persistence.Auditing;

/// <summary>Marks columns whose values must never be copied into <c>audit_log</c> (password hashes, tokens…).</summary>
public static class AuditPropertyExtensions
{
    private const string Annotation = "Ipm:ExcludeFromAudit";

    public static PropertyBuilder<T> ExcludeFromAudit<T>(this PropertyBuilder<T> property) =>
        property.HasAnnotation(Annotation, true);

    internal static bool IsExcludedFromAudit(this IReadOnlyProperty property) =>
        property.FindAnnotation(Annotation)?.Value is true;
}
