using Ipm.Domain.Common;

namespace Ipm.Domain.Identity;

/// <summary>
/// A named set of permissions. System roles are granted to users everywhere; project roles are granted inside a
/// project (part 04). <see cref="Denied"/> always wins over <see cref="Granted"/> (SRS §2.2, default deny).
/// Built-in roles (R15) can be edited but never deleted; <see cref="Key"/>, <see cref="Scope"/> and
/// <see cref="IsBuiltIn"/> never change after creation.
/// </summary>
public sealed class Role : AuditableEntity
{
    public const int KeyMaxLength = 40;
    public const int NameMaxLength = 60;
    public const int DescriptionMaxLength = 300;

    private Role() { }

    private Role(Guid id) : base(id) { }

    public string Key { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public RoleScope Scope { get; private set; }

    public bool IsBuiltIn { get; private set; }

    /// <summary>Granted permissions (a <c>text[]</c> column, so a change bumps the version and shows in audit_log).</summary>
    public List<string> Granted { get; private set; } = [];

    /// <summary>Explicitly denied permissions: they cancel a grant from any other role of the same user.</summary>
    public List<string> Denied { get; private set; } = [];

    /// <summary>What this role gives on its own: the grants that are not denied by the role itself.</summary>
    public IReadOnlyList<string> EffectivePermissions => [.. Granted.Where(p => !Denied.Contains(p))];

    /// <summary>A custom role. Its key is generated: <c>custom_</c> plus the start of its id.</summary>
    public static Result<Role> Create(string name, string description, RoleScope scope, IEnumerable<string> granted, IEnumerable<string> denied)
    {
        var role = new Role();
        role.Key = $"custom_{role.Id.ToString("N")[^12..]}";
        role.Scope = scope;
        role.Update(name, description);
        var result = role.SetPermissions(granted, denied);
        if (result.IsFailure)
        {
            return result.Error;
        }

        return role;
    }

    /// <summary>A built-in role (seed only). Fixed ids keep seeding idempotent.</summary>
    public static Role CreateBuiltIn(Guid id, string key, string name, string description, RoleScope scope, IEnumerable<string> granted)
    {
        var role = new Role(id) { Key = key, Scope = scope, IsBuiltIn = true };
        role.Update(name, description);
        var result = role.SetPermissions(granted, []);
        if (result.IsFailure)
        {
            throw new ArgumentException(result.Error.Description, nameof(granted));
        }

        return role;
    }

    public void Update(string name, string description)
    {
        Name = name.Trim();
        Description = description.Trim();
    }

    /// <summary>R18: only known keys, and a key cannot be both granted and denied.</summary>
    public Result SetPermissions(IEnumerable<string> granted, IEnumerable<string> denied)
    {
        var wantedGranted = granted.Distinct(StringComparer.Ordinal).ToList();
        var wantedDenied = denied.Distinct(StringComparer.Ordinal).ToList();

        var unknown = wantedGranted.Concat(wantedDenied).Where(p => !Permissions.IsKnown(p)).Distinct().ToList();
        if (unknown.Count > 0)
        {
            return RoleErrors.UnknownPermissions(unknown);
        }

        if (wantedGranted.Intersect(wantedDenied).Any())
        {
            return RoleErrors.PermissionConflict;
        }

        // New lists in catalog order, so EF Core always sees the change and the stored order is stable.
        Granted = [.. Permissions.All.Where(wantedGranted.Contains)];
        Denied = [.. Permissions.All.Where(wantedDenied.Contains)];
        return Result.Success();
    }

    /// <summary>Gives the role every known permission it neither holds nor denies (the seeder keeps <c>system_admin</c> complete).</summary>
    public void GrantMissing()
    {
        Granted = [.. Permissions.All.Where(p => Granted.Contains(p) || !Denied.Contains(p))];
    }

    /// <summary>R15. R16 (assigned to users) needs the database and is checked by the handler.</summary>
    public Result EnsureDeletable() => IsBuiltIn ? RoleErrors.BuiltInCannotBeDeleted : Result.Success();
}
