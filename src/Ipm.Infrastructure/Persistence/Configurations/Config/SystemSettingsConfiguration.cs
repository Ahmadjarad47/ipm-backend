using Ipm.Domain.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ipm.Infrastructure.Persistence.Configurations.Config;

internal sealed class SystemSettingsConfiguration : IEntityTypeConfiguration<SystemSettings>
{
    public void Configure(EntityTypeBuilder<SystemSettings> builder)
    {
        builder.ToTable("system_settings", Schemas.Config);
        builder.HasKey(s => s.Id);
        builder.Property(s => s.TimeZone).HasMaxLength(64);
    }
}
