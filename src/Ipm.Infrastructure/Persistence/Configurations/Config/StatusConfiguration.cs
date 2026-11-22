using Ipm.Domain.Config;
using Ipm.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ipm.Infrastructure.Persistence.Configurations.Config;

internal sealed class StatusConfiguration : IEntityTypeConfiguration<Status>
{
    public void Configure(EntityTypeBuilder<Status> builder)
    {
        var categories = string.Join(", ", KebabCaseEnumConverter<StatusCategory>.AllTexts.Select(t => $"'{t}'"));
        var scopes = string.Join(", ", KebabCaseEnumConverter<StatusScope>.AllTexts.Select(t => $"'{t}'"));

        builder.ToTable("statuses", Schemas.Config, t =>
        {
            t.HasCheckConstraint("ck_statuses_key", "key ~ '^[a-z][a-z0-9_]{1,30}$'");
            t.HasCheckConstraint("ck_statuses_color", "color ~ '^#[0-9A-Fa-f]{6}$'");
            t.HasCheckConstraint("ck_statuses_category", $"category IN ({categories})");
            t.HasCheckConstraint("ck_statuses_scope", $"scope IN ({scopes})");
            t.HasCheckConstraint("ck_statuses_sort_order", "sort_order >= 0");
        });

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Key).HasMaxLength(Status.KeyMaxLength);
        builder.Property(s => s.Name).HasMaxLength(Status.NameMaxLength);
        builder.Property(s => s.Description).HasMaxLength(Status.DescriptionMaxLength);
        builder.Property(s => s.Color).HasMaxLength(7);
        builder.Property(s => s.Category).HasConversion<KebabCaseEnumConverter<StatusCategory>>().HasMaxLength(16);
        builder.Property(s => s.Scope).HasConversion<KebabCaseEnumConverter<StatusScope>>().HasMaxLength(16);

        // R1: keys are stored in lower case, so a plain unique index is case-insensitive. Unique per scope.
        builder.HasIndex(s => new { s.Scope, s.Key }).IsUnique();
        builder.HasIndex(s => new { s.Scope, s.SortOrder });
    }
}
