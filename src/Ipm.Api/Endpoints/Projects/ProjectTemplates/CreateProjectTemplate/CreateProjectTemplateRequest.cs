using Ipm.Domain.Projects;

namespace Ipm.Api.Endpoints.Projects.ProjectTemplates.CreateProjectTemplate;

public sealed record CreateProjectTemplateRequest(
    string Name,
    string? Description,
    bool? Active,
    IReadOnlyList<Guid>? AllowedTypeIds,
    Guid DefaultTypeId,
    Guid? InitialStatusId,
    IReadOnlyList<Guid>? SuggestedRoleIds,
    int DurationDays,
    IReadOnlyList<SuggestedMember>? SuggestedMembers);
