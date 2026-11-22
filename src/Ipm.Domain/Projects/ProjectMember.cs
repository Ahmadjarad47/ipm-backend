using Ipm.Domain.Common;

namespace Ipm.Domain.Projects;

/// <summary>
/// A user inside a project with a project-scope role. The role grants nothing outside this project (FR-PRJ-05).
/// Members are changed only through <see cref="Project.SetMembers"/>, which also moves the project version (ETag).
/// </summary>
public sealed class ProjectMember : AuditableEntity
{
    private ProjectMember() { }

    internal ProjectMember(Guid projectId, Guid userId, Guid roleId)
    {
        ProjectId = projectId;
        UserId = userId;
        RoleId = roleId;
    }

    public Guid ProjectId { get; private set; }

    public Guid UserId { get; private set; }

    public Guid RoleId { get; private set; }

    internal void ChangeRole(Guid roleId) => RoleId = roleId;
}
