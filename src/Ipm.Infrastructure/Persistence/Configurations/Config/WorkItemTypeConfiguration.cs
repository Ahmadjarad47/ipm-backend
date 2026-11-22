using Ipm.Domain.Config;
using Ipm.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ipm.Infrastructure.Persistence.Configurations.Config;

internal sealed class WorkItemTypeConfiguration : IEntityTypeConfiguration<WorkItemType>
{
    public void Configure(EntityTypeBuilder<WorkItemType> builder)
    {
        var icons = string.Join(", ", KebabCaseEnumConverter<WorkItemIcon>.AllTexts.Select(t => $"'{t}'"));

        builder.ToTable("work_item_types", Schemas.Config, t =>
        {
            t.HasCheckConstraint("ck_work_item_types_key", "key ~ '^[a-z][a-z0-9_]{1,30}$'");
            t.HasCheckConstraint("ck_work_item_types_color", "color ~ '^#[0-9A-Fa-f]{6}$'");
            t.HasCheckConstraint("ck_work_item_types_icon", $"icon IN ({icons})");
            t.HasCheckConstraint("ck_work_item_types_sort_order", "sort_order >= 0");
        });

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Key).HasMaxLength(WorkItemType.KeyMaxLength);
        builder.Property(t => t.Name).HasMaxLength(WorkItemType.NameMaxLength);
        builder.Property(t => t.Description).HasMaxLength(WorkItemType.DescriptionMaxLength);
        builder.Property(t => t.Color).HasMaxLength(7);
        builder.Property(t => t.Icon).HasConversion<KebabCaseEnumConverter<WorkItemIcon>>().HasMaxLength(16);
        builder.Property(t => t.Fields).HasJsonbList();
        builder.Property(t => t.AllowedChildTypeIds).HasColumnName("allowed_child_type_ids");

        // R1: keys are stored in lower case, so a plain unique index is case-insensitive.
        builder.HasIndex(t => t.Key).IsUnique();
        builder.HasIndex(t => t.SortOrder);
    }
}
