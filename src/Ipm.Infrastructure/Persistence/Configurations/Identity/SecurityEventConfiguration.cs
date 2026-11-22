using Ipm.Domain.Identity;
using Ipm.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ipm.Infrastructure.Persistence.Configurations.Identity;

internal sealed class SecurityEventConfiguration : IEntityTypeConfiguration<SecurityEvent>
{
    public void Configure(EntityTypeBuilder<SecurityEvent> builder)
    {
        var types = string.Join(", ", KebabCaseEnumConverter<SecurityEventType>.AllTexts.Select(t => $"'{t}'"));

        builder.ToTable("security_events", Schemas.Identity, t => t.HasCheckConstraint("ck_security_events_type", $"type IN ({types})"));
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Type).HasConversion<KebabCaseEnumConverter<SecurityEventType>>().HasMaxLength(32);
        builder.Property(e => e.Username).HasMaxLength(SecurityEvent.UsernameMaxLength);
        builder.Property(e => e.Reason).HasMaxLength(SecurityEvent.ReasonMaxLength);
        builder.Property(e => e.Ip).HasMaxLength(45);
        builder.Property(e => e.Permission).HasMaxLength(64);

        builder.HasIndex(e => e.At);
        builder.HasIndex(e => new { e.UserId, e.At });
    }
}
