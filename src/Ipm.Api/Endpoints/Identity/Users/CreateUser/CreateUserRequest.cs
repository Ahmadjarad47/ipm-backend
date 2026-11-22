namespace Ipm.Api.Endpoints.Identity.Users.CreateUser;

public sealed record CreateUserRequest(
    string Username,
    string Name,
    string Email,
    string? Title,
    Guid? ManagerId,
    IReadOnlyList<Guid>? SystemRoleIds,
    string Password);
