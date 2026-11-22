namespace Ipm.Application.Features.Identity.Users.Queries.GetUserDirectory;

/// <summary>What every signed-in user may see about another user (R20).</summary>
public sealed record UserDirectoryItem(Guid Id, string Name, string Username, string Title, bool IsActive);
