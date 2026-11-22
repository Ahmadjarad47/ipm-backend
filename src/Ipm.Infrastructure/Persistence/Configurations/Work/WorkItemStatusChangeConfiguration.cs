using Ipm.Domain.Config;
using Ipm.Domain.Identity;
using Ipm.Domain.Work;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ipm.Infrastructure.Persistence.Configurations.Work;

internal sealed class WorkItemStatusChangeConfiguration : IEntityTypeConfiguration<WorkItemStatusChange>
{
    public void Configure(EntityTypeBuilder<WorkItemStatusChange> builder)
    {
        builder.ToTable("work_item_status_changes", Schemas.Work);
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Comment).HasMaxLength(WorkItemStatusChange.CommentMaxLength);

        builder.HasIndex(c => new { c.WorkItemId, c.At });

        builder.HasOne<WorkItem>().WithMany().HasForeignKey(c => c.WorkItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Status>().WithMany().HasForeignKey(c => c.FromStatusId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Status>().WithMany().HasForeignKey(c => c.ToStatusId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(c => c.ActorId).OnDelete(DeleteBehavior.Restrict);
    }
}
