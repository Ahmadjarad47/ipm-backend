namespace Ipm.Api.Endpoints.Identity.Users.UpdateUser;

public sealed record UpdateUserRequest(string Name, string Email, string? Title, Guid? ManagerId);
