using Ipm.Domain.Identity;
using Ipm.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ipm.Infrastructure.Persistence.Configurations.Identity;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        var known = string.Join(", ", Permissions.All.Select(p => $"'{p}'"));
        var scopes = string.Join(", ", KebabCaseEnumConverter<RoleScope>.AllTexts.Select(t => $"'{t}'"));

        builder.ToTable("roles", Schemas.Identity, t =>
        {
            t.HasCheckConstraint("ck_roles_scope", $"scope IN ({scopes})");
            t.HasCheckConstraint("ck_roles_key", "key ~ '^[a-z][a-z0-9_]*$'");
            // R18: only known keys, and none both granted and denied.
            t.HasCheckConstraint("ck_roles_permissions_known", $"permissions <@ ARRAY[{known}]::text[] AND denied <@ ARRAY[{known}]::text[]");
            t.HasCheckConstraint("ck_roles_no_conflict", "NOT (permissions && denied)");
        });

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Key).HasMaxLength(Role.KeyMaxLength);
        builder.Property(r => r.Name).HasMaxLength(Role.NameMaxLength);
        builder.Property(r => r.Description).HasMaxLength(Role.DescriptionMaxLength);
        builder.Property(r => r.Scope).HasConversion<KebabCaseEnumConverter<RoleScope>>().HasMaxLength(16);
        builder.Property(r => r.Granted).HasColumnName("permissions");
        builder.Property(r => r.Denied).HasColumnName("denied");
        builder.Ignore(r => r.EffectivePermissions);

        builder.HasIndex(r => r.Key).IsUnique();
        builder.HasIndex(r => r.Name).IsUnique();
    }
}
