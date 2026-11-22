namespace Ipm.Application.Features.Identity.Users.Common;

/// <summary>
/// One user. <see cref="Version"/> is sent as the ETag. Callers without <c>permissions.assign</c> get the directory
/// view (R20): no e-mail, manager or roles.
/// </summary>
public sealed record UserResponse(
    Guid Id,
    string Username,
    string Name,
    string Email,
    string Title,
    bool IsActive,
    Guid? ManagerId,
    IReadOnlyList<Guid> SystemRoleIds,
    bool MustChangePassword,
    uint Version);
