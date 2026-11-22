namespace Ipm.Application.Abstractions.Authentication;

/// <summary>
/// The one place that answers "may this user do this?". Cached in Redis (<c>perm:{userId}</c>, 10 minutes) and cleared
/// on every change that affects it, so a removed permission is refused on the very next request (not after the JWT expires).
/// </summary>
public interface IAccessService
{
    /// <summary>Null when the user does not exist.</summary>
    Task<UserAccess?> GetAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// System-wide permission, or (with <paramref name="projectId"/>) one granted by the user's role inside that project.
    /// Project membership arrives with part 04; until then a project id adds nothing to the system permissions.
    /// </summary>
    Task<bool> CanAsync(Guid userId, string permission, Guid? projectId = null, CancellationToken cancellationToken = default);

    Task InvalidateAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Clears every user holding the role (its permissions changed).</summary>
    Task InvalidateRoleAsync(Guid roleId, CancellationToken cancellationToken = default);
}
