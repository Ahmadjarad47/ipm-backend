using Ipm.Domain.Projects;
using Ipm.Domain.Work;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ipm.Infrastructure.Persistence.Configurations.Work;

internal sealed class ProjectCounterConfiguration : IEntityTypeConfiguration<ProjectCounter>
{
    public void Configure(EntityTypeBuilder<ProjectCounter> builder)
    {
        builder.ToTable("project_counters", Schemas.Work);
        builder.HasKey(c => c.ProjectId);
        builder.Property(c => c.ProjectId).ValueGeneratedNever();
        builder.HasOne<Project>().WithMany().HasForeignKey(c => c.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}
