using Ipm.Domain.Config;
using Ipm.Domain.Work;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ipm.Infrastructure.Persistence.Configurations.Work;

internal sealed class WorkItemRelationConfiguration : IEntityTypeConfiguration<WorkItemRelation>
{
    public void Configure(EntityTypeBuilder<WorkItemRelation> builder)
    {
        builder.ToTable("work_item_relations", Schemas.Work, t => t.HasCheckConstraint("ck_work_item_relations_self", "source_id <> target_id"));
        builder.HasKey(r => r.Id);

        // R4: the same link of the same type is stored once (the inverse is checked by the handler).
        builder.HasIndex(r => new { r.RelationTypeId, r.SourceId, r.TargetId }).IsUnique();
        builder.HasIndex(r => r.SourceId);
        builder.HasIndex(r => r.TargetId);

        builder.HasOne<WorkItem>().WithMany().HasForeignKey(r => r.SourceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkItem>().WithMany().HasForeignKey(r => r.TargetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RelationType>().WithMany().HasForeignKey(r => r.RelationTypeId).OnDelete(DeleteBehavior.Restrict);
    }
}
