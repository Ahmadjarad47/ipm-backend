namespace Ipm.Application.Features.Identity.Users.Queries.GetUsers;

public sealed record UserListItem(
    Guid Id,
    string Username,
    string Name,
    string Email,
    string Title,
    bool IsActive,
    Guid? ManagerId,
    IReadOnlyList<Guid> SystemRoleIds,
    bool IsLockedOut = false);
