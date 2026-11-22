using Ipm.Domain.Config;
using Ipm.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ipm.Infrastructure.Persistence.Configurations.Config;

internal sealed class WorkflowConfiguration : IEntityTypeConfiguration<Workflow>
{
    public void Configure(EntityTypeBuilder<Workflow> builder)
    {
        var scopes = string.Join(", ", KebabCaseEnumConverter<StatusScope>.AllTexts.Select(t => $"'{t}'"));

        builder.ToTable("workflows", Schemas.Config, t =>
        {
            t.HasCheckConstraint("ck_workflows_key", "key ~ '^[a-z][a-z0-9_]{1,30}$'");
            t.HasCheckConstraint("ck_workflows_scope", $"scope IN ({scopes})");
            // R7: the initial status is one of the workflow statuses.
            t.HasCheckConstraint("ck_workflows_initial_status", "initial_status_id = ANY (status_ids)");
        });

        builder.HasKey(w => w.Id);
        builder.Property(w => w.Key).HasMaxLength(Workflow.KeyMaxLength);
        builder.Property(w => w.Name).HasMaxLength(Workflow.NameMaxLength);
        builder.Property(w => w.Scope).HasConversion<KebabCaseEnumConverter<StatusScope>>().HasMaxLength(16);
        builder.Property(w => w.StatusIds).HasColumnName("status_ids");
        builder.Property(w => w.Transitions).HasJsonbList();

        builder.HasOne<Status>().WithMany().HasForeignKey(w => w.InitialStatusId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(w => w.Key).IsUnique();
    }
}
