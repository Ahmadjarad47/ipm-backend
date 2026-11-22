using Ipm.Domain.Projects;

namespace Ipm.Api.Endpoints.Projects.ProjectTemplates.UpdateProjectTemplate;

/// <summary>The lists are the complete lists. An omitted <c>active</c> keeps the current value.</summary>
public sealed record UpdateProjectTemplateRequest(
    string Name,
    string? Description,
    bool? Active,
    IReadOnlyList<Guid>? AllowedTypeIds,
    Guid DefaultTypeId,
    Guid? InitialStatusId,
    IReadOnlyList<Guid>? SuggestedRoleIds,
    int DurationDays,
    IReadOnlyList<SuggestedMember>? SuggestedMembers);
