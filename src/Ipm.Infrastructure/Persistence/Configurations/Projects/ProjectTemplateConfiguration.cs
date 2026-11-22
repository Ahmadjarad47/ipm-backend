using Ipm.Domain.Config;
using Ipm.Domain.Projects;
using Ipm.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ipm.Infrastructure.Persistence.Configurations.Projects;

internal sealed class ProjectTemplateConfiguration : IEntityTypeConfiguration<ProjectTemplate>
{
    public void Configure(EntityTypeBuilder<ProjectTemplate> builder)
    {
        builder.ToTable("project_templates", Schemas.Projects, t =>
        {
            t.HasCheckConstraint("ck_project_templates_duration", "duration_days >= 1");
            t.HasCheckConstraint("ck_project_templates_default_type_allowed", "default_type_id = ANY (allowed_type_ids)");
        });

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasMaxLength(ProjectTemplate.NameMaxLength);
        builder.Property(t => t.Description).HasMaxLength(ProjectTemplate.DescriptionMaxLength);
        builder.Property(t => t.SuggestedMembers).HasJsonbList();

        builder.HasIndex(t => t.Name).IsUnique();

        builder.HasOne<Status>().WithMany().HasForeignKey(t => t.InitialStatusId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkItemType>().WithMany().HasForeignKey(t => t.DefaultTypeId).OnDelete(DeleteBehavior.Restrict);
    }
}
