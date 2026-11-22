using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Projects.Projects.Common;

namespace Ipm.Application.Features.Projects.Projects.Commands.UnarchiveProject;

/// <summary>Restores an archived project so it can be edited again.</summary>
public sealed record UnarchiveProjectCommand(Guid Id, uint ExpectedVersion) : ICommand<ProjectResponse>;
