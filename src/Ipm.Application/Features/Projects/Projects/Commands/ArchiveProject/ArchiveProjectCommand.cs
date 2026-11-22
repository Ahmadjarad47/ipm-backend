using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Projects.Projects.Common;

namespace Ipm.Application.Features.Projects.Projects.Commands.ArchiveProject;

/// <summary>An archived project is read-only (R6) until it is restored.</summary>
public sealed record ArchiveProjectCommand(Guid Id, uint ExpectedVersion) : ICommand<ProjectResponse>;
