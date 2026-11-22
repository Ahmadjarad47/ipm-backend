using Ipm.Domain.Projects;

namespace Ipm.Application.Features.Projects.ProjectTemplates.Common;

/// <summary>One project template. <see cref="Version"/> is sent as the ETag.</summary>
public sealed record ProjectTemplateResponse(
    Guid Id,
    string Name,
    string Description,
    bool Active,
    IReadOnlyList<Guid> AllowedTypeIds,
    Guid DefaultTypeId,
    Guid InitialStatusId,
    IReadOnlyList<Guid> SuggestedRoleIds,
    int DurationDays,
    IReadOnlyList<SuggestedMember> SuggestedMembers,
    uint Version);
