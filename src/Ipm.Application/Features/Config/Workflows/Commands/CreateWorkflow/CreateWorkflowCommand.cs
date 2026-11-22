using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.Workflows.Common;
using Ipm.Domain.Config;

namespace Ipm.Application.Features.Config.Workflows.Commands.CreateWorkflow;

public sealed record CreateWorkflowCommand(
    string Key,
    string Name,
    StatusScope Scope,
    IReadOnlyList<Guid> StatusIds,
    Guid InitialStatusId,
    IReadOnlyList<WorkflowTransition> Transitions) : ICommand<WorkflowResponse>;
