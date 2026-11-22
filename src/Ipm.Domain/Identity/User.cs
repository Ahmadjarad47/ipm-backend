using Ipm.Domain.Common;
using Microsoft.AspNetCore.Identity;

namespace Ipm.Domain.Identity;

/// <summary>
/// A person who signs in: one table, <c>identity.users</c>. It is the ASP.NET Core Identity user, so password hash,
/// lockout and security stamp come from <see cref="IdentityUser{TKey}"/>. Because it cannot also inherit
/// <see cref="Entity"/>, it carries its own <see cref="Version"/> (PostgreSQL <c>xmin</c>) and audit stamps.
/// Users are never deleted, only deactivated (R8).
/// </summary>
public sealed class User : IdentityUser<Guid>, IAuditable
{
    public const int NameMaxLength = 100;
    public const int TitleMaxLength = 100;
    public const int EmailMaxLength = 256;
    public const int UsernameMaxLength = 31;

    private User() { }

    public string Name { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public bool IsActive { get; private set; } = true;

    /// <summary>Direct manager (resolves the <c>direct_manager</c> approver later). Must be active, not self, no cycles (R14).</summary>
    public Guid? ManagerId { get; private set; }

    /// <summary>System-scope role ids (a <c>uuid[]</c> column: a change bumps the version and shows in audit_log).</summary>
    public List<Guid> RoleIds { get; private set; } = [];

    /// <summary>Set when an administrator chose the password; cleared once the user picks their own (R6).</summary>
    public bool MustChangePassword { get; private set; }

    /// <summary>Concurrency token (PostgreSQL <c>xmin</c>), exposed as the ETag.</summary>
    public uint Version { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    /// <summary>Normalized forms come from <c>UserManager</c> (<c>NormalizeName</c> / <c>NormalizeEmail</c>).</summary>
    public static User Create(
        string username, string normalizedUsername, string name, string email, string normalizedEmail,
        string title, Guid? managerId, IEnumerable<Guid> roleIds, Guid? id = null)
    {
        var user = new User
        {
            Id = id ?? Guid.CreateVersion7(),
            SecurityStamp = Guid.NewGuid().ToString("N"),
            LockoutEnabled = true,
            UserName = username.Trim().ToLowerInvariant(),
            NormalizedUserName = normalizedUsername,
            RoleIds = [.. roleIds.Distinct()],
        };
        user.UpdateProfile(name, email, normalizedEmail, title, managerId);
        return user;
    }

    public bool HasVersion(uint expected) => Version == expected;

    public void UpdateProfile(string name, string email, string normalizedEmail, string title, Guid? managerId)
    {
        Name = name.Trim();
        Email = email.Trim();
        NormalizedEmail = normalizedEmail;
        Title = title.Trim();
        ManagerId = managerId;
    }

    /// <summary>R10: nobody changes their own roles.</summary>
    public Result ChangeRoles(Guid? currentUserId, IEnumerable<Guid> roleIds)
    {
        if (currentUserId == Id)
        {
            return UserErrors.CannotChangeOwnRoles;
        }

        RoleIds = [.. roleIds.Distinct()];
        return Result.Success();
    }

    public void Activate() => IsActive = true;

    /// <summary>R9: you cannot deactivate yourself. R13 (last role manager) needs the database.</summary>
    public Result Deactivate(Guid? currentUserId)
    {
        if (currentUserId == Id)
        {
            return UserErrors.CannotDeactivateSelf;
        }

        IsActive = false;
        return Result.Success();
    }

    public void PasswordSetByAdministrator() => MustChangePassword = true;

    public void PasswordChangedByOwner() => MustChangePassword = false;

    public void SetCreated(DateTimeOffset at, Guid? by)
    {
        CreatedAt = at;
        CreatedBy = by;
    }

    public void SetUpdated(DateTimeOffset at, Guid? by)
    {
        UpdatedAt = at;
        UpdatedBy = by;
    }
}
