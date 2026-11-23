using Ipm.Domain.Git;
using Ipm.Domain.Projects;
using Ipm.Infrastructure.Persistence.Auditing;
using Ipm.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ipm.Infrastructure.Persistence.Configurations.Git;

internal sealed class GitRepositoryConfiguration : IEntityTypeConfiguration<GitRepository>
{
    public void Configure(EntityTypeBuilder<GitRepository> builder)
    {
        var providers = string.Join(", ", KebabCaseEnumConverter<GitProvider>.AllTexts.Select(t => $"'{t}'"));

        builder.ToTable("repositories", Schemas.Git, t => t.HasCheckConstraint("ck_repositories_provider", $"provider IN ({providers})"));
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Provider).HasConversion<KebabCaseEnumConverter<GitProvider>>().HasMaxLength(16);
        builder.Property(r => r.Name).HasMaxLength(GitRepository.NameMaxLength);
        builder.Property(r => r.Url).HasMaxLength(500);
        builder.Property(r => r.DefaultBranch).HasMaxLength(200);
        builder.Property(r => r.LastSyncError).HasMaxLength(500);

        // R15: encrypted, and never written to the audit log.
        builder.Property(r => r.AccessTokenCipher).HasMaxLength(2000).ExcludeFromAudit();
        builder.Property(r => r.WebhookSecretCipher).HasMaxLength(2000).ExcludeFromAudit();

        // R1: a repository is connected to a project once; a disconnected record does not block connecting it again.
        builder.HasIndex(r => new { r.ProjectId, r.Name }).IsUnique().HasFilter("is_connected");
        builder.HasOne<Project>().WithMany().HasForeignKey(r => r.ProjectId).OnDelete(DeleteBehavior.Restrict);
    }
}
