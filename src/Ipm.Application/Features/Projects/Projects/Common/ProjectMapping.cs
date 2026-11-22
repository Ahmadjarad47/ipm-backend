using Ipm.Domain.Projects;

namespace Ipm.Application.Features.Projects.Projects.Common;

internal static class ProjectMapping
{
    public static ProjectResponse ToResponse(this Project project) => new(
        project.Id,
        project.Code,
        project.Name,
        project.Description,
        project.OwnerId,
        project.StatusId,
        project.StartDate,
        project.EndDate,
        project.IsArchived,
        [.. project.Members.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id).Select(m => new ProjectMemberResponse(m.UserId, m.RoleId))],
        project.AllowedTypeIds,
        project.DefaultTypeId,
        project.WorkflowOverrides.ToDictionary(o => o.TypeId, o => o.WorkflowId),
        project.Version);
}
