using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Projects.Projects.Common;
using Ipm.Domain.Projects;

namespace Ipm.Application.Features.Projects.Projects.Commands.SetProjectWorkflowOverrides;

/// <summary>Replaces the workflow overrides of the project: for a type, the workflow this project uses instead of the type own (R13).</summary>
public sealed record SetProjectWorkflowOverridesCommand(Guid Id, IReadOnlyList<WorkflowOverride> Overrides, uint ExpectedVersion)
    : ICommand<ProjectResponse>;
