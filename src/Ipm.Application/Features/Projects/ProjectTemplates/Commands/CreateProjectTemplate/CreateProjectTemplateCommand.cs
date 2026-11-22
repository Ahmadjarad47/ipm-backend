using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Projects.ProjectTemplates.Common;
using Ipm.Domain.Projects;

namespace Ipm.Application.Features.Projects.ProjectTemplates.Commands.CreateProjectTemplate;

/// <summary>An omitted <paramref name="InitialStatusId"/> means the initial status of the project workflow (R14).</summary>
public sealed record CreateProjectTemplateCommand(
    string Name,
    string Description,
    bool Active,
    IReadOnlyList<Guid> AllowedTypeIds,
    Guid DefaultTypeId,
    Guid? InitialStatusId,
    IReadOnlyList<Guid> SuggestedRoleIds,
    int DurationDays,
    IReadOnlyList<SuggestedMember> SuggestedMembers) : ICommand<ProjectTemplateResponse>;
