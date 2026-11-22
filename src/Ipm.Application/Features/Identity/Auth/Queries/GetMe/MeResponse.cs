namespace Ipm.Application.Features.Identity.Auth.Queries.GetMe;

/// <summary>
/// <see cref="SystemPermissions"/> are valid everywhere; <see cref="ProjectPermissions"/> (project id → keys) fill in with
/// project membership in part 04; <see cref="Denied"/> always wins over grants.
/// </summary>
public sealed record MeResponse(
    MeUser User,
    IReadOnlyList<string> SystemPermissions,
    IReadOnlyDictionary<string, IReadOnlyList<string>> ProjectPermissions,
    IReadOnlyList<string> Denied,
    bool MustChangePassword);
