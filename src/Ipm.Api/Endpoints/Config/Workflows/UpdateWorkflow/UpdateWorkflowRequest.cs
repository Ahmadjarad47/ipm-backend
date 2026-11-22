using Ipm.Domain.Config;

namespace Ipm.Api.Endpoints.Config.Workflows.UpdateWorkflow;

/// <summary><c>key</c> and <c>scope</c> are optional and only checked (a different value is refused). The statuses and transitions are the complete lists.</summary>
public sealed record UpdateWorkflowRequest(
    string Name,
    IReadOnlyList<Guid>? StatusIds,
    Guid InitialStatusId,
    IReadOnlyList<WorkflowTransition>? Transitions,
    string? Key,
    StatusScope? Scope);
