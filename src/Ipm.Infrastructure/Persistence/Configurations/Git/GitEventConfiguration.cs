using Ipm.Domain.Git;
using Ipm.Domain.Projects;
using Ipm.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ipm.Infrastructure.Persistence.Configurations.Git;

internal sealed class GitEventConfiguration : IEntityTypeConfiguration<GitEvent>
{
    public void Configure(EntityTypeBuilder<GitEvent> builder)
    {
        var kinds = string.Join(", ", KebabCaseEnumConverter<GitEventKind>.AllTexts.Select(t => $"'{t}'"));

        builder.ToTable("git_events", Schemas.Git, t => t.HasCheckConstraint("ck_git_events_kind", $"kind IN ({kinds})"));
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Kind).HasConversion<KebabCaseEnumConverter<GitEventKind>>().HasMaxLength(16);
        builder.Property(e => e.PrState).HasConversion<KebabCaseEnumConverter<PullRequestState>>().HasMaxLength(16);
        builder.Property(e => e.Title).HasMaxLength(GitEvent.TitleMaxLength);
        builder.Property(e => e.Branch).HasMaxLength(300);
        builder.Property(e => e.Sha).HasMaxLength(64);
        builder.Property(e => e.Author).HasMaxLength(200);
        builder.Property(e => e.Url).HasMaxLength(1000);
        builder.Property(e => e.DeliveryId).HasMaxLength(200);
        builder.Property(e => e.WorkItemRefs).HasColumnName("work_item_refs");
        builder.Property(e => e.ManualRefs).HasColumnName("manual_refs");

        // R6, R7: a commit and a pull request are stored once per repository.
        builder.HasIndex(e => new { e.RepositoryId, e.Sha }).IsUnique().HasFilter("kind = 'push'");
        builder.HasIndex(e => new { e.RepositoryId, e.PrNumber }).IsUnique().HasFilter("pr_number IS NOT NULL");
        builder.HasIndex(e => new { e.ProjectId, e.CreatedAt });
        builder.HasIndex(e => e.WorkItemRefs).HasMethod("gin");

        builder.HasOne<Project>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GitRepository>().WithMany().HasForeignKey(e => e.RepositoryId).OnDelete(DeleteBehavior.Restrict);
    }
}
