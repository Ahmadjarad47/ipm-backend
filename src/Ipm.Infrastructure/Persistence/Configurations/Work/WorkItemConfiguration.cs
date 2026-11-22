using Ipm.Domain.Config;
using Ipm.Domain.Projects;
using Ipm.Domain.Work;
using Ipm.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ipm.Infrastructure.Persistence.Configurations.Work;

internal sealed class WorkItemConfiguration : IEntityTypeConfiguration<WorkItem>
{
    public void Configure(EntityTypeBuilder<WorkItem> builder)
    {
        var priorities = string.Join(", ", KebabCaseEnumConverter<Priority>.AllTexts.Select(t => $"'{t}'"));

        builder.ToTable("work_items", Schemas.Work, t =>
        {
            t.HasCheckConstraint("ck_work_items_priority", $"priority IN ({priorities})");
            t.HasCheckConstraint("ck_work_items_dates", "start_date IS NULL OR due_date IS NULL OR due_date >= start_date");
            t.HasCheckConstraint("ck_work_items_parent", "parent_id IS NULL OR parent_id <> id");
        });

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Ref).HasMaxLength(Project.CodeMaxLength + 12);
        builder.Property(i => i.Title).HasMaxLength(WorkItem.TitleMaxLength);
        builder.Property(i => i.Description).HasMaxLength(WorkItem.DescriptionMaxLength);
        builder.Property(i => i.Priority).HasConversion<KebabCaseEnumConverter<Priority>>().HasMaxLength(16);
        builder.Property(i => i.AssigneeIds).HasColumnName("assignee_ids");
        builder.Property(i => i.Fields).HasJsonbDictionary();

        // R1: the number is unique inside its project, the ref across all projects.
        builder.HasIndex(i => new { i.ProjectId, i.Number }).IsUnique();
        builder.HasIndex(i => i.Ref).IsUnique();
        builder.HasIndex(i => new { i.ProjectId, i.StatusId });
        builder.HasIndex(i => i.ParentId);
        builder.HasIndex(i => new { i.ProjectId, i.IsArchived });
        builder.HasIndex(i => i.TypeId);
        builder.HasIndex(i => i.AssigneeIds).HasMethod("gin");

        builder.HasOne<Project>().WithMany().HasForeignKey(i => i.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkItemType>().WithMany().HasForeignKey(i => i.TypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Status>().WithMany().HasForeignKey(i => i.StatusId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkItem>().WithMany().HasForeignKey(i => i.ParentId).OnDelete(DeleteBehavior.Restrict);
    }
}
