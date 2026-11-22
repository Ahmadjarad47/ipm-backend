using Ipm.Domain.Approvals;
using Ipm.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ipm.Infrastructure.Persistence.Configurations.Approvals;

internal sealed class DelegationConfiguration : IEntityTypeConfiguration<Delegation>
{
    public void Configure(EntityTypeBuilder<Delegation> builder)
    {
        builder.ToTable("delegations", Schemas.Approvals, t => t.HasCheckConstraint("ck_delegations_dates", "to_date >= from_date"));
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Reason).HasMaxLength(Delegation.ReasonMaxLength);

        builder.HasIndex(d => new { d.FromUserId, d.IsActive });
        builder.HasIndex(d => d.ToUserId);

        builder.HasOne<User>().WithMany().HasForeignKey(d => d.FromUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(d => d.ToUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
