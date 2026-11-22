using System.Text.Json.Serialization;

namespace Ipm.Application.Abstractions.Authentication;

/// <summary>
/// What a user may do right now: active flag, security stamp, whether the password must be changed, and the
/// permissions of all their system roles. <see cref="Denied"/> always wins over <see cref="Granted"/>.
/// </summary>
public sealed record UserAccess(
    Guid UserId,
    bool IsActive,
    string SecurityStamp,
    bool MustChangePassword,
    IReadOnlyList<string> Granted,
    IReadOnlyList<string> Denied)
{
    /// <summary>Granted and not denied, in the order of <see cref="Granted"/>.</summary>
    [JsonIgnore]
    public IReadOnlyList<string> Effective => [.. Granted.Where(p => !Denied.Contains(p))];

    public bool Has(string permission) => IsActive && !Denied.Contains(permission) && Granted.Contains(permission);
}
