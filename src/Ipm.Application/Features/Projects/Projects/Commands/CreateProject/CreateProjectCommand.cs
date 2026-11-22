using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Projects.Projects.Common;

namespace Ipm.Application.Features.Projects.Projects.Commands.CreateProject;

/// <summary>
/// The project starts in the initial status of the project workflow (R4). The owner and the creator become project managers; a
/// template (R5) adds its allowed types and suggested members.
/// </summary>
public sealed record CreateProjectCommand(
    string Code,
    string Name,
    string Description,
    Guid OwnerId,
    DateOnly StartDate,
    DateOnly EndDate,
    Guid? TemplateId) : ICommand<ProjectResponse>;
