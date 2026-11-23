using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ipm.Infrastructure.Persistence.Auditing;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("audit_log", "public");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Entity).HasMaxLength(128);
        builder.Property(e => e.Action).HasConversion<string>().HasMaxLength(16);
        builder.Property(e => e.Before).HasColumnType("jsonb");
        builder.Property(e => e.After).HasColumnType("jsonb");
        builder.Property(e => e.SubjectLabel).HasMaxLength(200);
        builder.Property(e => e.Source).HasMaxLength(16);
        builder.Property(e => e.CorrelationId).HasMaxLength(64);
        builder.Property(e => e.PrevHash).HasMaxLength(64);
        builder.Property(e => e.Hash).HasMaxLength(64);
        builder.HasIndex(e => e.Seq).IsUnique();
        builder.HasIndex(e => new { e.ProjectId, e.At });
        builder.HasIndex(e => new { e.WorkItemId, e.At });
        builder.HasIndex(e => e.CorrelationId);

        // "History of this record" and "what did this user change".
        builder.HasIndex(e => new { e.Entity, e.EntityId, e.At });
        builder.HasIndex(e => new { e.UserId, e.At });
    }
}
