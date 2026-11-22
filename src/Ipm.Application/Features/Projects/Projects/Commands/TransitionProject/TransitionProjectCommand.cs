using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Projects.Projects.Common;

namespace Ipm.Application.Features.Projects.Projects.Commands.TransitionProject;

/// <summary>Moves the project to another status through the project workflow (R9 R10 R11).</summary>
public sealed record TransitionProjectCommand(Guid Id, Guid ToStatusId, string? Comment, uint ExpectedVersion) : ICommand<ProjectResponse>;
