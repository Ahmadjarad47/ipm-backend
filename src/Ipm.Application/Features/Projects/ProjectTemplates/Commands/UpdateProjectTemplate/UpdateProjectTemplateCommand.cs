using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Projects.ProjectTemplates.Common;
using Ipm.Domain.Projects;

namespace Ipm.Application.Features.Projects.ProjectTemplates.Commands.UpdateProjectTemplate;

/// <summary>The lists are the complete lists. An omitted <paramref name="Active"/> keeps the current value; changing a template never touches existing projects.</summary>
public sealed record UpdateProjectTemplateCommand(
    Guid Id,
    string Name,
    string Description,
    bool? Active,
    IReadOnlyList<Guid> AllowedTypeIds,
    Guid DefaultTypeId,
    Guid? InitialStatusId,
    IReadOnlyList<Guid> SuggestedRoleIds,
    int DurationDays,
    IReadOnlyList<SuggestedMember> SuggestedMembers,
    uint ExpectedVersion) : ICommand<ProjectTemplateResponse>;
