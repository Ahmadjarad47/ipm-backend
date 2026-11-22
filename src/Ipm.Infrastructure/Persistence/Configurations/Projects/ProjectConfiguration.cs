using Ipm.Domain.Config;
using Ipm.Domain.Identity;
using Ipm.Domain.Projects;
using Ipm.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ipm.Infrastructure.Persistence.Configurations.Projects;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects", Schemas.Projects, t =>
        {
            t.HasCheckConstraint("ck_projects_code", "code ~ '^[A-Z][A-Z0-9]{1,9}$'");
            t.HasCheckConstraint("ck_projects_dates", "end_date >= start_date");
            t.HasCheckConstraint("ck_projects_default_type_allowed", "default_type_id = ANY (allowed_type_ids)");
        });

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Code).HasMaxLength(Project.CodeMaxLength);
        builder.Property(p => p.Name).HasMaxLength(Project.NameMaxLength);
        builder.Property(p => p.Description).HasMaxLength(4000);
        builder.Property(p => p.WorkflowOverrides).HasJsonbList();

        // R1: codes are stored in upper case, so a plain unique index is case-insensitive.
        builder.HasIndex(p => p.Code).IsUnique();
        builder.HasIndex(p => p.OwnerId);
        builder.HasIndex(p => p.StatusId);

        builder.HasOne<User>().WithMany().HasForeignKey(p => p.OwnerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Status>().WithMany().HasForeignKey(p => p.StatusId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkItemType>().WithMany().HasForeignKey(p => p.DefaultTypeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Members).WithOne().HasForeignKey(m => m.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Members).HasField("_members").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
