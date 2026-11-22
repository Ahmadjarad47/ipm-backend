using Ipm.Domain.Projects;

namespace Ipm.Application.Features.Projects.ProjectTemplates.Common;

internal static class ProjectTemplateMapping
{
    public static ProjectTemplateResponse ToResponse(this ProjectTemplate template) => new(
        template.Id,
        template.Name,
        template.Description,
        template.IsActive,
        template.AllowedTypeIds,
        template.DefaultTypeId,
        template.InitialStatusId,
        template.SuggestedRoleIds,
        template.DurationDays,
        template.SuggestedMembers,
        template.Version);
}
