using Ipm.Domain.Config;
using Ipm.Domain.Identity;
using Ipm.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ipm.Infrastructure.Persistence.Configurations.Projects;

internal sealed class ProjectStatusChangeConfiguration : IEntityTypeConfiguration<ProjectStatusChange>
{
    public void Configure(EntityTypeBuilder<ProjectStatusChange> builder)
    {
        builder.ToTable("project_status_changes", Schemas.Projects);
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Comment).HasMaxLength(ProjectStatusChange.CommentMaxLength);

        builder.HasIndex(c => new { c.ProjectId, c.At });

        builder.HasOne<Project>().WithMany().HasForeignKey(c => c.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Status>().WithMany().HasForeignKey(c => c.FromStatusId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Status>().WithMany().HasForeignKey(c => c.ToStatusId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(c => c.ActorId).OnDelete(DeleteBehavior.Restrict);
    }
}
