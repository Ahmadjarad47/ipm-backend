namespace Ipm.Application.Features.Identity.Auth.Queries.GetMe;

public sealed record MeUser(Guid Id, string Name, string Username, string Email, string Title);
