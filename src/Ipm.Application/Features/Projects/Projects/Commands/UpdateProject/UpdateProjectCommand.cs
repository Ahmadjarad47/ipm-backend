using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Projects.Projects.Common;

namespace Ipm.Application.Features.Projects.Projects.Commands.UpdateProject;

/// <summary>
/// <paramref name="Code"/> is optional and only checked (it can never change, R1). <paramref name="StatusId"/> is optional and only
/// checked: the status changes through transitions only (R9). An omitted <paramref name="OwnerId"/> keeps the current owner.
/// </summary>
public sealed record UpdateProjectCommand(
    Guid Id,
    string? Code,
    Guid? StatusId,
    string Name,
    string Description,
    DateOnly StartDate,
    DateOnly EndDate,
    Guid? OwnerId,
    uint ExpectedVersion) : ICommand<ProjectResponse>;
