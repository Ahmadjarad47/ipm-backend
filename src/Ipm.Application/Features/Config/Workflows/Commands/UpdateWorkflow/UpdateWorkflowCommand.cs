using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.Workflows.Common;
using Ipm.Domain.Config;

namespace Ipm.Application.Features.Config.Workflows.Commands.UpdateWorkflow;

/// <summary>
/// The statuses and transitions are the complete lists. <paramref name="Key"/> and <paramref name="Scope"/> are optional and only
/// checked: they can never change (R5 R2).
/// </summary>
public sealed record UpdateWorkflowCommand(
    Guid Id,
    string? Key,
    StatusScope? Scope,
    string Name,
    IReadOnlyList<Guid> StatusIds,
    Guid InitialStatusId,
    IReadOnlyList<WorkflowTransition> Transitions,
    uint ExpectedVersion) : ICommand<WorkflowResponse>;
