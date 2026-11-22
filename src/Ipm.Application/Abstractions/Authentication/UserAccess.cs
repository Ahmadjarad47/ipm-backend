using System.Text.Json.Serialization;

namespace Ipm.Application.Abstractions.Authentication;

/// <summary>
/// What a user may do right now: active flag, security stamp, whether the password must be changed, the permissions of all their
/// system roles, and (in <see cref="ProjectPermissions"/>) what their role gives them inside each project they are a member of.
/// An explicit denial always wins, then a system grant, then a grant inside the project; everything else is refused.
/// </summary>
public sealed record UserAccess(
    Guid UserId,
    bool IsActive,
    string SecurityStamp,
    bool MustChangePassword,
    IReadOnlyList<string> Granted,
    IReadOnlyList<string> Denied)
{
    /// <summary>Project id to the permissions of the user project role there. A cached copy without it reads as no project permissions.</summary>
    public IReadOnlyDictionary<Guid, IReadOnlyList<string>> ProjectPermissions { get; init; } = new Dictionary<Guid, IReadOnlyList<string>>();

    /// <summary>Granted and not denied, in the order of <see cref="Granted"/>.</summary>
    [JsonIgnore]
    public IReadOnlyList<string> Effective => [.. Granted.Where(p => !Denied.Contains(p))];

    /// <summary>The system-wide permission only.</summary>
    public bool Has(string permission) => IsActive && !Denied.Contains(permission) && Granted.Contains(permission);

    /// <summary>The permission system-wide, or (when <paramref name="projectId"/> is given) through the user role in that project.</summary>
    public bool Can(string permission, Guid? projectId)
    {
        if (!IsActive || Denied.Contains(permission))
        {
            return false;
        }

        if (Granted.Contains(permission))
        {
            return true;
        }

        return projectId is { } id && ProjectPermissions.TryGetValue(id, out var inProject) && inProject.Contains(permission);
    }

    /// <summary>What the user may do inside one project: the system permissions plus their project role, minus denials.</summary>
    public IReadOnlyList<string> EffectiveIn(Guid projectId) =>
        ProjectPermissions.TryGetValue(projectId, out var inProject)
            ? [.. Effective.Union(inProject.Where(p => !Denied.Contains(p)))]
            : Effective;
}
