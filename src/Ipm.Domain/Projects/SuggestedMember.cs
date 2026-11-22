namespace Ipm.Domain.Projects;

/// <summary>A member a project template adds on creation. Without a <paramref name="UserId"/> it only suggests the role.</summary>
public sealed record SuggestedMember(Guid? UserId, Guid RoleId);
