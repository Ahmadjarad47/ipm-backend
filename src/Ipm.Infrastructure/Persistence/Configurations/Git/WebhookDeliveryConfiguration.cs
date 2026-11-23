using Ipm.Domain.Git;
using Ipm.Domain.Projects;
using Ipm.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ipm.Infrastructure.Persistence.Configurations.Git;

internal sealed class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> builder)
    {
        var outcomes = string.Join(", ", KebabCaseEnumConverter<DeliveryOutcome>.AllTexts.Select(t => $"'{t}'"));

        builder.ToTable("webhook_deliveries", Schemas.Git, t => t.HasCheckConstraint("ck_webhook_deliveries_outcome", $"outcome IN ({outcomes})"));
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.Provider).HasConversion<KebabCaseEnumConverter<GitProvider>>().HasMaxLength(16);
        builder.Property(d => d.Outcome).HasConversion<KebabCaseEnumConverter<DeliveryOutcome>>().HasMaxLength(16);
        builder.Property(d => d.DeliveryId).HasMaxLength(200);
        builder.Property(d => d.Event).HasMaxLength(100);
        builder.Property(d => d.Message).HasMaxLength(WebhookDelivery.MessageMaxLength);
        builder.Property(d => d.LinkedRefs).HasColumnName("linked_refs");

        // R6: a delivery id is handled once. A refused or repeated call does not use it up.
        builder.HasIndex(d => new { d.RepositoryId, d.DeliveryId }).IsUnique().HasFilter("outcome IN ('processed', 'failed')");
        builder.HasIndex(d => new { d.ProjectId, d.CreatedAt });

        builder.HasOne<Project>().WithMany().HasForeignKey(d => d.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GitRepository>().WithMany().HasForeignKey(d => d.RepositoryId).OnDelete(DeleteBehavior.Restrict);
    }
}
