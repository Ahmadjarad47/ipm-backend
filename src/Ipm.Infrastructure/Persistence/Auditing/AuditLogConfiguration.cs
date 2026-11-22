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

        // "History of this record" and "what did this user change".
        builder.HasIndex(e => new { e.Entity, e.EntityId, e.At });
        builder.HasIndex(e => new { e.UserId, e.At });
    }
}
