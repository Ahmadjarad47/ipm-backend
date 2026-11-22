using Ipm.Domain.Config;

namespace Ipm.Api.Endpoints.Config.Workflows.CreateWorkflow;

public sealed record CreateWorkflowRequest(
    string Key,
    string Name,
    StatusScope Scope,
    IReadOnlyList<Guid>? StatusIds,
    Guid InitialStatusId,
    IReadOnlyList<WorkflowTransition>? Transitions);
