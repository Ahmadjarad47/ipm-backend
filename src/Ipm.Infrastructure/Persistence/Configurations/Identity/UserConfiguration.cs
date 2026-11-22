using Ipm.Domain.Identity;
using Ipm.Infrastructure.Persistence.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ipm.Infrastructure.Persistence.Configurations.Identity;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", Schemas.Identity, t =>
            t.HasCheckConstraint("ck_users_user_name", "user_name ~ '^[a-z][a-z0-9._-]{2,30}$'"));

        builder.Property(u => u.Name).HasMaxLength(User.NameMaxLength);
        builder.Property(u => u.Title).HasMaxLength(User.TitleMaxLength);
        builder.Property(u => u.UserName).HasMaxLength(User.UsernameMaxLength);
        builder.Property(u => u.Email).HasMaxLength(User.EmailMaxLength);
        builder.Property(u => u.Version).IsRowVersion();
        builder.Property(u => u.RoleIds).HasColumnName("role_ids");

        // R7: the Identity indexes are not unique by default.
        builder.HasIndex(u => u.NormalizedUserName).IsUnique().HasDatabaseName("ix_users_normalized_user_name");
        builder.HasIndex(u => u.NormalizedEmail).IsUnique().HasDatabaseName("ix_users_normalized_email");
        builder.HasIndex(u => u.ManagerId);

        // R14: the reporting line.
        builder.HasOne<User>().WithMany().HasForeignKey(u => u.ManagerId).OnDelete(DeleteBehavior.Restrict);

        // Secrets never reach audit_log.
        builder.Property(u => u.PasswordHash).ExcludeFromAudit();
        builder.Property(u => u.SecurityStamp).ExcludeFromAudit();
        builder.Property(u => u.ConcurrencyStamp).ExcludeFromAudit();

        // Identity columns we do not use.
        builder.Ignore(u => u.PhoneNumber);
        builder.Ignore(u => u.PhoneNumberConfirmed);
        builder.Ignore(u => u.TwoFactorEnabled);
    }
}
