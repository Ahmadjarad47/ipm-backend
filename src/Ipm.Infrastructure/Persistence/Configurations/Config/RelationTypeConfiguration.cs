using Ipm.Domain.Config;
using Ipm.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ipm.Infrastructure.Persistence.Configurations.Config;

internal sealed class RelationTypeConfiguration : IEntityTypeConfiguration<RelationType>
{
    public void Configure(EntityTypeBuilder<RelationType> builder)
    {
        builder.ToTable("relation_types", Schemas.Config, t =>
        {
            t.HasCheckConstraint("ck_relation_types_key", "key ~ '^[a-z][a-z0-9_]{1,30}$'");
            // R7: an undirected type reads the same from both sides and cannot forbid cycles.
            t.HasCheckConstraint("ck_relation_types_undirected_name", "is_directed OR inverse_name = name");
            t.HasCheckConstraint("ck_relation_types_acyclic_directed", "is_directed OR NOT is_acyclic");
        });

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Key).HasMaxLength(31);
        builder.Property(t => t.Name).HasMaxLength(RelationType.NameMaxLength);
        builder.Property(t => t.InverseName).HasMaxLength(RelationType.NameMaxLength);
        builder.Property(t => t.AllowedPairs).HasJsonbList();

        builder.HasIndex(t => t.Key).IsUnique();
    }
}
