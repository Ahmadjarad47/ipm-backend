namespace Ipm.Domain.Identity;

/// <summary>System roles are granted to a user everywhere; project roles only inside a project the user is a member of (part 04).</summary>
public enum RoleScope
{
    System = 0,
    Project = 1,
}
